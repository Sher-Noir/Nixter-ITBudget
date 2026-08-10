using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using LedgerForge.Setup.Engine;

namespace LedgerForge.Setup;

public partial class MainWindow
{
    private SetupOperation _selectedOperation = SetupOperation.Install;
    private ExistingInstallation? _existingInstallation;

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _existingInstallation = await DetectExistingInstallationAsync();
            if (_existingInstallation is null)
            {
                _selectedOperation = SetupOperation.Install;
                OperationGroup.Visibility = Visibility.Collapsed;
                InstallButton.Content = "Install";
                return;
            }

            ApplyExistingInstallation(_existingInstallation);
            _selectedOperation = SetupOperation.Upgrade;
            UpgradeRadio.IsChecked = true;
            OperationGroup.Visibility = Visibility.Visible;
            ExistingInstallBanner.Visibility = Visibility.Visible;
            SetupTitleText.Text = "Maintain LedgerForge";
            SetupSubtitleText.Text = "Upgrade or repair the existing deployment without replacing its data or host configuration.";
            PreflightText.Text = "Existing installation detected. Run prerequisite checks before upgrading or reinstalling.";
            UpdateOperationUi();
        }
        catch (Exception exception)
        {
            AppendLog($"Existing-installation detection could not complete: {exception.Message}");
        }
    }

    private void Operation_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateOperationUi();
    }

    private void UpdateOperationUi()
    {
        if (_existingInstallation is null)
        {
            _selectedOperation = SetupOperation.Install;
            InstallButton.Content = "Install";
            return;
        }

        _selectedOperation = ReinstallRadio.IsChecked == true
            ? SetupOperation.Reinstall
            : SetupOperation.Upgrade;
        InstallButton.Content = _selectedOperation == SetupOperation.Upgrade ? "Upgrade" : "Reinstall";

        var installed = _existingInstallation.InstalledVersion ?? "unknown";
        ExistingInstallText.Text =
            $"Installed LedgerForge version: {installed}. This Setup package: {GetSetupPackageVersion()}. " +
            (_selectedOperation == SetupOperation.Upgrade
                ? "Upgrade requires a newer package and preserves the existing deployment configuration and data."
                : "Reinstall forces a clean application-payload replacement while preserving the existing deployment configuration and data.");
    }

    private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_preflightPassed)
        {
            MessageBox.Show(this, "Run the prerequisite check first.", "LedgerForge Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SetupPlan plan;
        try
        {
            plan = BuildPlan();
            var errors = SetupPlanValidator.Validate(plan);
            if (errors.Count != 0)
            {
                MessageBox.Show(this, string.Join(Environment.NewLine, errors), "Review setup configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsLocalSqlServer(plan.SqlServer))
            {
                MessageBox.Show(
                    this,
                    "This Setup currently automates SQL Server or SQL Express on the same Windows server. Remote SQL Server deployments require the application identity/login to be provisioned separately.",
                    "Local SQL Server required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_selectedOperation == SetupOperation.Upgrade &&
                _existingInstallation is not null &&
                !PackageIsNewer(_existingInstallation.InstalledVersion, GetSetupPackageVersion()))
            {
                MessageBox.Show(
                    this,
                    $"Upgrade requires a newer Setup package. Installed: {_existingInstallation.InstalledVersion ?? "unknown"}; package: {GetSetupPackageVersion()}. Choose Reinstall / repair if you intentionally want to force this payload over the existing installation.",
                    "Newer release required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Review setup configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        CheckButton.IsEnabled = false;
        InstallButton.IsEnabled = false;
        StatusText.Text = _selectedOperation switch
        {
            SetupOperation.Upgrade => "Upgrading...",
            SetupOperation.Reinstall => "Reinstalling...",
            _ => "Installing..."
        };

        try
        {
            if (_selectedOperation == SetupOperation.Install)
                await InstallAsync(plan);
            else
                await MaintainExistingInstallationAsync(plan, _selectedOperation);

            await SaveInstallStateAsync(plan, GetSetupPackageVersion());
            StatusText.Text = _selectedOperation switch
            {
                SetupOperation.Upgrade => "Upgrade completed.",
                SetupOperation.Reinstall => "Reinstall completed.",
                _ => "Installation completed."
            };
            PreflightBanner.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(223, 246, 221));
            PreflightText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 87, 36));
            PreflightText.Text = $"LedgerForge is healthy at http://localhost:{plan.HttpPort}/health. Open http://localhost:{plan.HttpPort}/ to continue.";
            MessageBox.Show(
                this,
                _selectedOperation switch
                {
                    SetupOperation.Upgrade => "LedgerForge was upgraded successfully. The existing database, documents, and host configuration were preserved.",
                    SetupOperation.Reinstall => "LedgerForge was reinstalled successfully. The existing database, documents, and host configuration were preserved.",
                    _ => "LedgerForge Setup completed successfully."
                },
                "LedgerForge Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            AppendLog($"ERROR  {exception.GetType().Name}: {exception.Message}");
            StatusText.Text = "Setup operation failed. Review the log.";
            MessageBox.Show(this, exception.Message, "LedgerForge Setup failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            CheckButton.IsEnabled = true;
            InstallButton.IsEnabled = _preflightPassed;
        }
    }

    private async Task MaintainExistingInstallationAsync(SetupPlan plan, SetupOperation operation)
    {
        var existing = _existingInstallation
            ?? throw new InvalidOperationException("An existing LedgerForge installation is required for this operation.");
        var staging = Path.Combine(Path.GetTempPath(), "LedgerForge-Setup-" + Guid.NewGuid().ToString("N"));
        var installRoot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(plan.InstallPath.Trim()));
        var webRoot = Path.Combine(installRoot, "Web");
        var bootstrapRoot = Path.Combine(installRoot, "Bootstrap");
        var productionSettingsPath = Path.Combine(webRoot, "appsettings.Production.json");

        if (!File.Exists(productionSettingsPath))
            throw new FileNotFoundException("The existing LedgerForge production configuration could not be found. Setup will not guess or replace it during maintenance.", productionSettingsPath);

        var productionSettings = await File.ReadAllTextAsync(productionSettingsPath);

        try
        {
            AppendLog($"Preparing embedded deployment payload for {operation.ToString().ToLowerInvariant()}...");
            Directory.CreateDirectory(staging);
            ExtractPayload(staging);
            var payloadWeb = Path.Combine(staging, "web");
            var payloadBootstrap = Path.Combine(staging, "bootstrap");
            if (!Directory.Exists(payloadWeb) || !Directory.Exists(payloadBootstrap))
                throw new InvalidDataException("The LedgerForge deployment payload is incomplete.");

            AppendLog("Stopping the existing IIS deployment...");
            await EnsureApplicationPoolAsync(plan);
            await StopExistingDeploymentAsync(plan);

            AppendLog("Replacing Web and Bootstrap payloads cleanly; SQL, documents, and .ledgerforge host data are not touched...");
            await DeleteDirectoryWithRetryAsync(webRoot);
            await DeleteDirectoryWithRetryAsync(bootstrapRoot);
            Directory.CreateDirectory(webRoot);
            Directory.CreateDirectory(bootstrapRoot);
            CopyDirectory(payloadWeb, webRoot);
            CopyDirectory(payloadBootstrap, bootstrapRoot);

            AppendLog("Restoring the existing production configuration exactly as it was before payload replacement...");
            await File.WriteAllTextAsync(productionSettingsPath, productionSettings, new UTF8Encoding(false));

            var documentsRoot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(plan.DocumentsPath.Trim()));
            Directory.CreateDirectory(documentsRoot);
            await RunProcessAsync("icacls.exe", [webRoot, "/grant", $"{plan.ApplicationPoolIdentity}:(OI)(CI)(RX)", "/T", "/C"]);
            await RunProcessAsync("icacls.exe", [documentsRoot, "/grant", $"{plan.ApplicationPoolIdentity}:(OI)(CI)(M)", "/T", "/C"]);

            var bootstrapExecutable = Path.Combine(bootstrapRoot, "LedgerForge.Bootstrap.exe");
            if (!File.Exists(bootstrapExecutable))
                throw new FileNotFoundException("LedgerForge.Bootstrap.exe is missing from the deployment payload.", bootstrapExecutable);

            AppendLog("Applying committed forward database migrations. Existing database records are preserved...");
            var bootstrapEnvironment = new Dictionary<string, string>
            {
                ["LEDGERFORGE_CONNECTION_STRING"] = existing.ConnectionString,
                ["LEDGERFORGE_DATABASE_APP_IDENTITY"] = plan.ApplicationPoolIdentity
            };
            var initialize = await RunProcessAsync(bootstrapExecutable, ["initialize"], bootstrapEnvironment);
            AppendProcessOutput(initialize);

            AppendLog("Preserving existing IIS bindings while refreshing the application path and authentication settings...");
            await ConfigureExistingSiteForMaintenanceAsync(plan, webRoot);
            await EnsureIisDeploymentStartedAsync(plan);

            AppendLog("Verifying migrated database state...");
            var verify = await RunProcessAsync(bootstrapExecutable, ["verify"], bootstrapEnvironment);
            AppendProcessOutput(verify);

            AppendLog("Waiting for the LedgerForge health endpoint...");
            await WaitForHealthAsync(plan);
            AppendLog($"PASS  LedgerForge {operation.ToString().ToLowerInvariant()} completed and health verification succeeded.");
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            }
            catch
            {
                // Temporary setup content is best-effort cleanup only.
            }
        }
    }

    private async Task ConfigureExistingSiteForMaintenanceAsync(SetupPlan plan, string webRoot)
    {
        var existingSite = await RunProcessAsync(AppCmdPath, ["list", "site", $"/name:{plan.SiteName}"], throwOnFailure: false);
        if (string.IsNullOrWhiteSpace(existingSite.Output))
        {
            await ConfigureSiteAsync(plan, webRoot);
            return;
        }

        await RunProcessAsync(AppCmdPath, ["set", "app", $"{plan.SiteName}/", $"/applicationPool:{plan.ApplicationPoolName}"]);
        await RunProcessAsync(AppCmdPath, ["set", "vdir", $"{plan.SiteName}/", $"/physicalPath:{webRoot}"]);
        await RunProcessAsync(
            AppCmdPath,
            ["set", "config", plan.SiteName, "-section:system.webServer/security/authentication/anonymousAuthentication", "/enabled:true", "/commit:apphost"]);
        await RunProcessAsync(
            AppCmdPath,
            ["set", "config", plan.SiteName, "-section:system.webServer/security/authentication/windowsAuthentication", "/enabled:true", "/commit:apphost"]);
    }

    private static async Task DeleteDirectoryWithRetryAsync(string path)
    {
        if (!Directory.Exists(path)) return;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(350));
            }
            catch (UnauthorizedAccessException) when (attempt < 5)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(350));
            }
        }

        Directory.Delete(path, recursive: true);
    }

    private async Task<ExistingInstallation?> DetectExistingInstallationAsync()
    {
        var statePath = GetInstallStatePath();
        InstallState? state = null;
        if (File.Exists(statePath))
        {
            try
            {
                var stateJson = await File.ReadAllTextAsync(statePath);
                state = JsonSerializer.Deserialize<InstallState>(stateJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
            {
                AppendLog($"Install-state metadata could not be read; falling back to deployment discovery: {exception.Message}");
            }
        }

        if (state is not null && !string.IsNullOrWhiteSpace(state.InstallPath))
        {
            var fromState = await ReadExistingInstallationAsync(state.InstallPath, state);
            if (fromState is not null) return fromState;
        }

        return await ReadExistingInstallationAsync(InstallPathText.Text, state: null);
    }

    private async Task<ExistingInstallation?> ReadExistingInstallationAsync(string installPath, InstallState? state)
    {
        if (string.IsNullOrWhiteSpace(installPath)) return null;
        var installRoot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(installPath.Trim()));
        var webRoot = Path.Combine(installRoot, "Web");
        var settingsPath = Path.Combine(webRoot, "appsettings.Production.json");
        if (!File.Exists(settingsPath)) return null;

        var json = await File.ReadAllTextAsync(settingsPath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var connectionString = GetJsonString(root, "ConnectionStrings", "LedgerForge");
        if (string.IsNullOrWhiteSpace(connectionString)) return null;

        var connection = new DbConnectionStringBuilder { ConnectionString = connectionString };
        var sqlServer = GetConnectionValue(connection, "Server", "Data Source") ?? ".\\SQLEXPRESS";
        var database = GetConnectionValue(connection, "Database", "Initial Catalog") ?? "LedgerForge";
        var organization = GetJsonString(root, "Branding", "OrganizationName") ?? "Your Organization";
        var documentsPath = GetJsonString(root, "Documents", "StoragePath")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LedgerForge", "Documents");
        var adminGroup = GetFirstArrayValue(root, "Security", "AdGroups", "SystemAdministrator");
        var installedVersion = state?.InstalledVersion ?? ReadInstalledWebVersion(webRoot);

        return new ExistingInstallation(
            installedVersion,
            installRoot,
            documentsPath,
            state?.SiteName ?? "LedgerForge",
            state?.HttpPort ?? 8080,
            state?.HostName ?? "localhost",
            sqlServer,
            database,
            organization,
            adminGroup,
            connectionString);
    }

    private void ApplyExistingInstallation(ExistingInstallation existing)
    {
        OrganizationNameText.Text = existing.OrganizationName;
        SiteNameText.Text = existing.SiteName;
        PortText.Text = existing.HttpPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        InstallPathText.Text = existing.InstallPath;
        DocumentsPathText.Text = existing.DocumentsPath;
        SqlServerText.Text = existing.SqlServer;
        DatabaseNameText.Text = existing.DatabaseName;
        AdminGroupText.Text = existing.SystemAdministratorGroup ?? string.Empty;

        OrganizationNameText.IsReadOnly = true;
        SiteNameText.IsReadOnly = true;
        PortText.IsReadOnly = true;
        InstallPathText.IsReadOnly = true;
        DocumentsPathText.IsReadOnly = true;
        SqlServerText.IsReadOnly = true;
        DatabaseNameText.IsReadOnly = true;
        InitialAdminText.IsReadOnly = true;
        AdminGroupText.IsReadOnly = true;
        InitialAdminHelpText.Text = "Upgrade/Reinstall does not create a new administrator grant. Existing security configuration is preserved.";
    }

    private async Task SaveInstallStateAsync(SetupPlan plan, string installedVersion)
    {
        var path = GetInstallStatePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var state = new InstallState(
            installedVersion,
            Path.GetFullPath(Environment.ExpandEnvironmentVariables(plan.InstallPath.Trim())),
            Path.GetFullPath(Environment.ExpandEnvironmentVariables(plan.DocumentsPath.Trim())),
            plan.SiteName.Trim(),
            plan.HttpPort,
            string.IsNullOrWhiteSpace(plan.HostName) ? "localhost" : plan.HostName.Trim());
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, new UTF8Encoding(false));
        AppendLog($"Saved non-secret install metadata to {path} for future upgrade detection.");
    }

    private static string GetInstallStatePath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LedgerForge", "install-state.json");

    private static string GetSetupPackageVersion()
    {
        var assembly = typeof(MainWindow).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return informational.Split('+', 2)[0].Trim();
        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static bool PackageIsNewer(string? installedVersion, string packageVersion)
    {
        if (!TryParseVersion(installedVersion, out var installed)) return true;
        if (!TryParseVersion(packageVersion, out var package)) return true;
        return package > installed;
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        var normalized = (value ?? string.Empty).Trim().TrimStart('v', 'V');
        var metadataIndex = normalized.IndexOf('+');
        if (metadataIndex >= 0) normalized = normalized[..metadataIndex];
        var prereleaseIndex = normalized.IndexOf('-');
        if (prereleaseIndex >= 0) normalized = normalized[..prereleaseIndex];
        return Version.TryParse(normalized, out version!);
    }

    private static string? ReadInstalledWebVersion(string webRoot)
    {
        var assemblyPath = Path.Combine(webRoot, "LedgerForge.Web.dll");
        if (!File.Exists(assemblyPath)) return null;
        var version = FileVersionInfo.GetVersionInfo(assemblyPath).ProductVersion;
        return string.IsNullOrWhiteSpace(version) ? null : version.Split('+', 2)[0];
    }

    private static string? GetJsonString(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                return null;
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static string? GetFirstArrayValue(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                return null;
        }
        if (current.ValueKind != JsonValueKind.Array) return null;
        foreach (var item in current.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                return item.GetString();
        }
        return null;
    }

    private static string? GetConnectionValue(DbConnectionStringBuilder builder, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (builder.TryGetValue(key, out var value) && value is not null)
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        return null;
    }

    private enum SetupOperation
    {
        Install,
        Upgrade,
        Reinstall
    }

    private sealed record InstallState(
        string InstalledVersion,
        string InstallPath,
        string DocumentsPath,
        string SiteName,
        int HttpPort,
        string HostName);

    private sealed record ExistingInstallation(
        string? InstalledVersion,
        string InstallPath,
        string DocumentsPath,
        string SiteName,
        int HttpPort,
        string HostName,
        string SqlServer,
        string DatabaseName,
        string OrganizationName,
        string? SystemAdministratorGroup,
        string ConnectionString);
}
