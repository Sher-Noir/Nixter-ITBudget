using Xunit;

namespace LedgerForge.UiTests;

public sealed class UpdateAndUpgradeContractTests
{
    [Fact]
    public void Setup_OffersUpgradeAndReinstallForExistingDeployments()
    {
        var xaml = Read("src", "LedgerForge.Setup", "MainWindow.xaml");
        var maintenance = Read("src", "LedgerForge.Setup", "MainWindow.Maintenance.cs");

        Assert.Contains("Upgrade —", xaml, StringComparison.Ordinal);
        Assert.Contains("Reinstall / repair", xaml, StringComparison.Ordinal);
        Assert.Contains("DetectExistingInstallationAsync", maintenance, StringComparison.Ordinal);
        Assert.Contains("PackageIsNewer", maintenance, StringComparison.Ordinal);
        Assert.Contains("install-state.json", maintenance, StringComparison.Ordinal);
    }

    [Fact]
    public void SetupMaintenance_PreservesDataConfigurationAndIisBindings()
    {
        var maintenance = Read("src", "LedgerForge.Setup", "MainWindow.Maintenance.cs");

        Assert.Contains("appsettings.Production.json", maintenance, StringComparison.Ordinal);
        Assert.Contains("Restoring the existing production configuration exactly", maintenance, StringComparison.Ordinal);
        Assert.Contains("DeleteDirectoryWithRetryAsync(webRoot)", maintenance, StringComparison.Ordinal);
        Assert.Contains("DeleteDirectoryWithRetryAsync(bootstrapRoot)", maintenance, StringComparison.Ordinal);
        Assert.Contains("ConfigureExistingSiteForMaintenanceAsync", maintenance, StringComparison.Ordinal);
        Assert.Contains("set\", \"vdir", maintenance, StringComparison.Ordinal);
        Assert.Contains("/enabled:true", maintenance, StringComparison.Ordinal);
        Assert.DoesNotContain("LEDGERFORGE_INITIAL_ADMIN_IDENTITY", maintenance, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateNotification_UsesCachedServerSideStableReleaseCheck()
    {
        var program = Read("src", "LedgerForge.Web", "Program.cs");
        var updateService = Read("src", "LedgerForge.Web", "Updates", "UpdateCheckService.cs");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "UpdatesController.cs");
        var ui = Read("src", "LedgerForge.Web", "wwwroot", "js", "ledgerforge-ui.js");
        var settings = Read("src", "LedgerForge.Web", "appsettings.json");

        Assert.Contains("AddHostedService<GitHubReleaseUpdateService>()", program, StringComparison.Ordinal);
        Assert.Contains("/releases/latest", updateService, StringComparison.Ordinal);
        Assert.Contains("CheckIntervalHours", updateService, StringComparison.Ordinal);
        Assert.Contains("HttpRequestException", updateService, StringComparison.Ordinal);
        Assert.Contains("[Authorize]", controller, StringComparison.Ordinal);
        Assert.Contains("/updates/status", ui, StringComparison.Ordinal);
        Assert.Contains("Update ${status.latestVersion}", ui, StringComparison.Ordinal);
        Assert.Contains("\"Enabled\": true", settings, StringComparison.Ordinal);
        Assert.Contains("Sher-Noir/Nixter-ITBudget", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void TaggedRelease_StampsWebAndSetupWithReleaseVersion()
    {
        var release = Read(".github", "workflows", "release.yml");

        Assert.Contains("LEDGERFORGE_RELEASE_VERSION", release, StringComparison.Ordinal);
        Assert.Contains("/p:Version=$env:LEDGERFORGE_RELEASE_VERSION", release, StringComparison.Ordinal);
        Assert.Contains("applicationVersion", release, StringComparison.Ordinal);
        Assert.Contains("LedgerForge.Setup.exe.sha256", release, StringComparison.Ordinal);
        Assert.Contains("release-manifest.json", release, StringComparison.Ordinal);
    }

    private static string Read(params string[] segments)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine([root, .. segments]));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LedgerForge.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("LedgerForge repository root could not be located from the test output directory.");
    }
}
