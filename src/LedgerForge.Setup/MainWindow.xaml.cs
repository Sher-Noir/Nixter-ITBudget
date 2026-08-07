using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Media;
using LedgerForge.Setup.Engine;

namespace LedgerForge.Setup;

public partial class MainWindow : Window
{
    private const string PayloadResourceName = "LedgerForge.Payload.zip";
    private bool _preflightPassed;

    public MainWindow()
    {
        InitializeComponent();
        InstallPathText.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LedgerForge");
        DocumentsPathText.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LedgerForge", "Documents");
        InitialAdminText.Text = WindowsIdentity.GetCurrent().Name ?? string.Empty;
    }

    private string AppCmdPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "System32",
        "inetsrv",
        "appcmd.exe");

    private string HostingModulePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "IIS",
        "Asp.Net Core Module",
        "V2",
        "aspnetcorev2.dll");

    private async void CheckButton_Click(object sender, RoutedEventArgs e)
    {
        CheckButton.IsEnabled = false;
        try
        {
            var checks = await CheckPrerequisitesAsync();
            var failures = checks.Where(x => !x.Success).ToArray();
            foreach (var check in checks)
                AppendLog($"{(check.Success ? "PASS" : "FAIL")}  {check.Name}: {check.Detail}");

            _preflightPassed = failures.Length == 0;
            InstallButton.IsEnabled = _preflightPassed;
            if (_preflightPassed)
            {
                PreflightBanner.Background = new SolidColorBrush(Color.FromRgb(223, 246, 221));
                PreflightText.Foreground = new SolidColorBrush(Color.FromRgb(21, 87, 36));
                PreflightText.Text = "Prerequisites passed. LedgerForge Setup can continue.";
                StatusText.Text = "Prerequisites passed.";
            }
            else
            {
                PreflightBanner.Background = new SolidColorBrush(Color.FromRgb(253, 231, 233));
                PreflightText.Foreground = new SolidColorBrush(Color.FromRgb(132, 32, 41));
                PreflightText.Text = "A prerequisite is missing. Enable IIS with Windows Authentication and install the .NET 10 ASP.NET Core Hosting Bundle, then run the check again.";
                StatusText.Text = "Prerequisites need attention.";
            }
        }
        catch (Exception exception)
        {
            AppendLog($"Prerequisite check failed: {exception.Message}");
            StatusText.Text = "Prerequisite check failed.";
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
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
                    "This Setup Preview currently supports a SQL Server or SQL Express instance on the same Windows server. Remote SQL Server service-account provisioning will be added in Advanced mode.",
                    "Local SQL Server required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
        StatusText.Text = "Installing...";
        try
        {
            await InstallAsync(plan);
            StatusText.Text = "Installation completed.";
            PreflightBanner.Background = new SolidColorBrush(Color.FromRgb(223, 246, 221));
            PreflightText.Foreground = new SolidColorBrush(Color.FromRgb(21, 87, 36));
            PreflightText.Text = $"LedgerForge is responding at http://localhost:{plan.HttpPort}/health. Open http://localhost:{plan.HttpPort}/ to sign in with Windows Authentication.";
            MessageBox.Show(this, "LedgerForge Setup Preview completed successfully.", "LedgerForge Setup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            AppendLog($"ERROR  {exception.GetType().Name}: {exception.Message}");
            StatusText.Text = "Installation failed. Review the log.";
            MessageBox.Show(this, exception.Message, "LedgerForge Setup failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            CheckButton.IsEnabled = true;
            InstallButton.IsEnabled = _preflightPassed;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private SetupPlan BuildPlan()
    {
        if (!int.TryParse(PortText.Text, out var port))
            throw new ArgumentException("HTTP port must be a number.");

        return new SetupPlan(
            OrganizationNameText.Text,
            SiteNameText.Text,
            InstallPathText.Text,
            DocumentsPathText.Text,
            SqlServerText.Text,
            DatabaseNameText.Text,
            InitialAdminText.Text,
            string.IsNullOrWhiteSpace(AdminGroupText.Text) ? null : AdminGroupText.Text,
            port,
            "localhost");
    }

    private async Task<IReadOnlyList<PrerequisiteCheck>> CheckPrerequisitesAsync()
    {
        var checks = new List<PrerequisiteCheck>();
        var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
        checks.Add(new(
            "Administrator elevation",
            principal.IsInRole(WindowsBuiltInRole.Administrator),
            principal.IsInRole(WindowsBuiltInRole.Administrator) ? "Setup is elevated." : "Run Setup as an administrator."));

        checks.Add(new(
            "IIS",
            File.Exists(AppCmdPath),
            File.Exists(AppCmdPath) ? AppCmdPath : "IIS management tools were not found."));

        checks.Add(new(
            "ASP.NET Core Hosting Bundle",
            File.Exists(HostingModulePath),
            File.Exists(HostingModulePath) ? HostingModulePath : "ASP.NET Core Module V2 was not found. Install the .NET 10 Hosting Bundle after IIS is enabled."));

        checks.Add(new(
            "Embedded deployment payload",
            HasPayload(),
            HasPayload() ? "The signed-build payload is embedded in Setup." : "This Setup executable was built without a deployment payload."));

        if (File.Exists(AppCmdPath))
        {
            var authCheck = await RunProcessAsync(
                AppCmdPath,
                ["list", "config", "/section:system.webServer/security/authentication/windowsAuthentication"],
                throwOnFailure: false);
            checks.Add(new(
                "IIS Windows Authentication",
                authCheck.ExitCode == 0,
                authCheck.ExitCode == 0 ? "Windows Authentication configuration is available." : "Enable the IIS Windows Authentication feature."));
        }

        return checks;
    }

    private async Task InstallAsync(SetupPlan plan)
    {
        var staging = Path.Combine(Path.GetTempPath(), "LedgerForge-Setup-" + Guid.NewGuid().ToString("N"));
        try
        {
            AppendLog("Preparing embedded deployment payload...");
            Directory.CreateDirectory(staging);
            ExtractPayload(staging);

            var payloadWeb = Path.Combine(staging, "web");
            var payloadBootstrap = Path.Combine(staging, "bootstrap");
            if (!Directory.Exists(payloadWeb) || !Directory.Exists(payloadBootstrap))
                throw new InvalidDataException("The LedgerForge deployment payload is incomplete.");

            var installRoot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(plan.InstallPath.Trim()));
            var documentsRoot = Path.GetFullPath(Environment.ExpandEnvironmentVariables(plan.DocumentsPath.Trim()));
            var webRoot = Path.Combine(installRoot, "Web");
            var bootstrapRoot = Path.Combine(installRoot, "Bootstrap");

            AppendLog("Preparing IIS application pool...");
            await EnsureApplicationPoolAsync(plan);
            await StopExistingDeploymentAsync(plan);

            AppendLog($"Installing application files to {installRoot}...");
            Directory.CreateDirectory(webRoot);
            Directory.CreateDirectory(bootstrapRoot);
            CopyDirectory(payloadWeb, webRoot);
            CopyDirectory(payloadBootstrap, bootstrapRoot);

            AppendLog($"Preparing non-web-root document storage at {documentsRoot}...");
            Directory.CreateDirectory(documentsRoot);

            AppendLog("Writing deployment configuration...");
            var productionSettings = DeploymentConfigurationRenderer.RenderProductionSettings(plan);
            await File.WriteAllTextAsync(Path.Combine(webRoot, "appsettings.Production.json"), productionSettings, new UTF8Encoding(false));

            AppendLog("Applying filesystem permissions for the IIS application pool identity...");
            await RunProcessAsync("icacls.exe", [webRoot, "/grant", $"{plan.ApplicationPoolIdentity}:(OI)(CI)(RX)", "/T", "/C"]);
            await RunProcessAsync("icacls.exe", [documentsRoot, "/grant", $"{plan.ApplicationPoolIdentity}:(OI)(CI)(M)", "/T", "/C"]);

            var bootstrapExecutable = Path.Combine(bootstrapRoot, "LedgerForge.Bootstrap.exe");
            if (!File.Exists(bootstrapExecutable))
                throw new FileNotFoundException("LedgerForge.Bootstrap.exe is missing from the deployment payload.", bootstrapExecutable);

            AppendLog("Applying committed database migrations and generic bootstrap data...");
            var bootstrapEnvironment = new Dictionary<string, string>
            {
                ["LEDGERFORGE_CONNECTION_STRING"] = DeploymentConfigurationRenderer.CreateConnectionString(plan),
                ["LEDGERFORGE_DATABASE_APP_IDENTITY"] = plan.ApplicationPoolIdentity,
                ["LEDGERFORGE_INITIAL_ADMIN_IDENTITY"] = plan.InitialAdministratorIdentity.Trim()
            };
            var bootstrap = await RunProcessAsync(bootstrapExecutable, ["initialize"], bootstrapEnvironment);
            AppendProcessOutput(bootstrap);

            AppendLog("Configuring IIS site and Windows Authentication...");
            await ConfigureSiteAsync(plan, webRoot);

            AppendLog("Starting LedgerForge...");
            await RunProcessAsync(AppCmdPath, ["start", "apppool", $"/apppool.name:{plan.ApplicationPoolName}"], throwOnFailure: false);
            await RunProcessAsync(AppCmdPath, ["start", "site", $"/site.name:{plan.SiteName}"], throwOnFailure: false);

            AppendLog("Verifying database state with the deployment bootstrap...");
            var verify = await RunProcessAsync(bootstrapExecutable, ["verify"], bootstrapEnvironment);
            AppendProcessOutput(verify);

            AppendLog("Waiting for the LedgerForge health endpoint...");
            await WaitForHealthAsync(plan.HttpPort);
            AppendLog("PASS  LedgerForge health endpoint is ready.");
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

    private async Task EnsureApplicationPoolAsync(SetupPlan plan)
    {
        var existing = await RunProcessAsync(AppCmdPath, ["list", "apppool", $"/name:{plan.ApplicationPoolName}"], throwOnFailure: false);
        if (string.IsNullOrWhiteSpace(existing.Output))
            await RunProcessAsync(AppCmdPath, ["add", "apppool", $"/name:{plan.ApplicationPoolName}"]);

        await RunProcessAsync(
            AppCmdPath,
            ["set", "apppool", plan.ApplicationPoolName, "/managedRuntimeVersion:", "/managedPipelineMode:Integrated"]);
    }

    private async Task StopExistingDeploymentAsync(SetupPlan plan)
    {
        var site = await RunProcessAsync(AppCmdPath, ["list", "site", $"/name:{plan.SiteName}"], throwOnFailure: false);
        if (!string.IsNullOrWhiteSpace(site.Output))
            await RunProcessAsync(AppCmdPath, ["stop", "site", $"/site.name:{plan.SiteName}"], throwOnFailure: false);

        await RunProcessAsync(AppCmdPath, ["stop", "apppool", $"/apppool.name:{plan.ApplicationPoolName}"], throwOnFailure: false);
    }

    private async Task ConfigureSiteAsync(SetupPlan plan, string webRoot)
    {
        var existing = await RunProcessAsync(AppCmdPath, ["list", "site", $"/name:{plan.SiteName}"], throwOnFailure: false);
        if (!string.IsNullOrWhiteSpace(existing.Output))
            await RunProcessAsync(AppCmdPath, ["delete", "site", plan.SiteName]);

        var hostName = string.IsNullOrWhiteSpace(plan.HostName) ? string.Empty : plan.HostName.Trim();
        await RunProcessAsync(
            AppCmdPath,
            ["add", "site", $"/name:{plan.SiteName}", $"/physicalPath:{webRoot}", $"/bindings:http/*:{plan.HttpPort}:{hostName}"]);
        await RunProcessAsync(AppCmdPath, ["set", "app", $"{plan.SiteName}/", $"/applicationPool:{plan.ApplicationPoolName}"]);
        await RunProcessAsync(
            AppCmdPath,
            ["set", "config", plan.SiteName, "-section:system.webServer/security/authentication/anonymousAuthentication", "/enabled:false", "/commit:apphost"]);
        await RunProcessAsync(
            AppCmdPath,
            ["set", "config", plan.SiteName, "-section:system.webServer/security/authentication/windowsAuthentication", "/enabled:true", "/commit:apphost"]);
    }

    private async Task WaitForHealthAsync(int port)
    {
        using var handler = new HttpClientHandler
        {
            UseDefaultCredentials = true,
            AllowAutoRedirect = false
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var uri = new Uri($"http://localhost:{port}/health");

        Exception? lastException = null;
        for (var attempt = 1; attempt <= 30; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(uri);
                if ((int)response.StatusCode == 200) return;
                lastException = new InvalidOperationException($"Health endpoint returned HTTP {(int)response.StatusCode}.");
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                lastException = exception;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new InvalidOperationException("LedgerForge did not become healthy after IIS deployment.", lastException);
    }

    private bool HasPayload()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResourceName);
        return stream is not null;
    }

    private static bool IsLocalSqlServer(string server)
    {
        var value = server.Trim();
        if (value == "." || value.StartsWith(@".\", StringComparison.OrdinalIgnoreCase)) return true;
        if (value.Equals("(local)", StringComparison.OrdinalIgnoreCase) || value.StartsWith(@"(local)\", StringComparison.OrdinalIgnoreCase)) return true;
        if (value.Equals("localhost", StringComparison.OrdinalIgnoreCase) || value.StartsWith(@"localhost\", StringComparison.OrdinalIgnoreCase)) return true;
        if (value.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase) || value.StartsWith(Environment.MachineName + "\\", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var destinationFile = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile, overwrite: true);
        }
    }

    private static void ExtractPayload(string destination)
    {
        using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResourceName)
            ?? throw new InvalidDataException("LedgerForge Setup does not contain an embedded deployment payload.");
        using var archive = new ZipArchive(payload, ZipArchiveMode.Read, leaveOpen: false);
        archive.ExtractToDirectory(destination, overwriteFiles: true);
    }

    private async Task<ProcessResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        bool throwOnFailure = true)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        if (environment is not null)
        {
            foreach (var pair in environment) startInfo.Environment[pair.Key] = pair.Value;
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to start {Path.GetFileName(fileName)}.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var result = new ProcessResult(process.ExitCode, await standardOutput, await standardError);

        if (throwOnFailure && result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
            throw new InvalidOperationException($"{Path.GetFileName(fileName)} failed with exit code {result.ExitCode}: {detail.Trim()}");
        }

        return result;
    }

    private void AppendProcessOutput(ProcessResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Output)) AppendLog(result.Output.Trim());
        if (!string.IsNullOrWhiteSpace(result.Error)) AppendLog(result.Error.Trim());
    }

    private void AppendLog(string message)
    {
        LogText.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogText.ScrollToEnd();
    }

    private sealed record PrerequisiteCheck(string Name, bool Success, string Detail);
    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
