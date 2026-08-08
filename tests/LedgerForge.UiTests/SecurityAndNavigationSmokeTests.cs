using Xunit;

namespace LedgerForge.UiTests;

public sealed class SecurityAndNavigationSmokeTests
{
    [Fact]
    public void WebHost_PreservesFailClosedSecurityMiddleware()
    {
        var program = Read("src", "LedgerForge.Web", "Program.cs");

        Assert.Contains("AddAuthentication(NegotiateDefaults.AuthenticationScheme)", program, StringComparison.Ordinal);
        Assert.Contains("UseAuthentication()", program, StringComparison.Ordinal);
        Assert.Contains("UseAuthorization()", program, StringComparison.Ordinal);
        Assert.Contains("Content-Security-Policy", program, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", program, StringComparison.Ordinal);
        Assert.Contains("AutoValidateAntiforgeryTokenAttribute", program, StringComparison.Ordinal);
    }

    [Fact]
    public void IisWebConfig_DoesNotOverrideServerAuthenticationSections()
    {
        var webConfig = Read("src", "LedgerForge.Web", "web.config");

        Assert.Contains("AspNetCoreModuleV2", webConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("anonymousAuthentication", webConfig, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("windowsAuthentication", webConfig, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ErrorEndpoints_CanHandleReExecutedPostRequests()
    {
        var home = Read("src", "LedgerForge.Web", "Controllers", "HomeController.cs");

        Assert.Contains("public IActionResult AccessDenied", home, StringComparison.Ordinal);
        Assert.Contains("public IActionResult Error()", home, StringComparison.Ordinal);
        Assert.DoesNotContain("[HttpGet]\n    public IActionResult AccessDenied", home, StringComparison.Ordinal);
        Assert.DoesNotContain("[HttpGet]\n    public IActionResult Error()", home, StringComparison.Ordinal);
    }

    [Fact]
    public void OrganizationOverrides_UseWritableNonWebRootHostStorage()
    {
        var store = Read("src", "LedgerForge.Web", "Configuration", "OrganizationSettingsStore.cs");

        Assert.Contains("Documents:StoragePath", store, StringComparison.Ordinal);
        Assert.Contains(".ledgerforge", store, StringComparison.Ordinal);
        Assert.Contains("Organization settings storage cannot be inside the public web root", store, StringComparison.Ordinal);
    }

    [Fact]
    public void PrimaryNavigation_ExposesCoreLedgerForgeModules()
    {
        var layout = Read("src", "LedgerForge.Web", "Views", "Shared", "_Layout.cshtml");
        var routes = new[]
        {
            "/work", "/budget", "/actuals", "/purchase-orders", "/invoices", "/vendors",
            "/contracts", "/renewals", "/documents", "/approvals", "/reports", "/fiscal-years",
            "/imports", "/admin/finance", "/admin/lookups", "/admin/organization",
            "/admin/security", "/admin/diagnostics"
        };

        Assert.All(routes, route => Assert.Contains($"href=\"{route}\"", layout, StringComparison.Ordinal));
    }

    [Fact]
    public void FinancialExports_AreServerGeneratedRoutes()
    {
        var exports = Read("src", "LedgerForge.Web", "Controllers", "ExportsController.cs");
        Assert.Contains("budget.csv", exports, StringComparison.Ordinal);
        Assert.Contains("actuals.csv", exports, StringComparison.Ordinal);
        Assert.Contains("finance.csv", exports, StringComparison.Ordinal);
        Assert.Contains("commitments.csv", exports, StringComparison.Ordinal);
        Assert.Contains("renewals.csv", exports, StringComparison.Ordinal);
        Assert.Contains("forecast.csv", exports, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicDefaults_AreOrganizationNeutral()
    {
        var settings = Read("src", "LedgerForge.Web", "appsettings.json");
        Assert.Contains("Your Organization", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("Charles River Community" + " Health", settings, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CR" + "CH", settings, StringComparison.OrdinalIgnoreCase);
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
