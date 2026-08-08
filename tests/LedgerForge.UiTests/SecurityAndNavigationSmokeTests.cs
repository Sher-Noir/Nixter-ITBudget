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
    public void PrimaryNavigation_MatchesFinancialWorkspaceModules()
    {
        var layout = Read("src", "LedgerForge.Web", "Views", "Shared", "_Layout.cshtml");
        var routes = new[]
        {
            "/budget", "/actuals", "/purchase-orders", "/invoices", "/vendors",
            "/contracts", "/renewals", "/documents", "/approvals", "/reports",
            "/fiscal-years", "/admin/organization"
        };

        Assert.All(routes, route => Assert.Contains($"href=\"{route}\"", layout, StringComparison.Ordinal));
        Assert.Contains(">Administration</span>", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void GlobalHeaderSearch_UsesExistingCrossModuleSearchRoute()
    {
        var layout = Read("src", "LedgerForge.Web", "Views", "Shared", "_Layout.cshtml");
        var search = Read("src", "LedgerForge.Web", "Controllers", "SearchController.cs");

        Assert.Contains("action=\"/search\"", layout, StringComparison.Ordinal);
        Assert.Contains("name=\"q\"", layout, StringComparison.Ordinal);
        Assert.Contains("Budget Items", search, StringComparison.Ordinal);
        Assert.Contains("Purchase Orders", search, StringComparison.Ordinal);
        Assert.Contains("Invoices", search, StringComparison.Ordinal);
        Assert.Contains("Contracts", search, StringComparison.Ordinal);
    }

    [Fact]
    public void AdministrationContext_ExposesOperationalAndAdminDestinations()
    {
        var layout = Read("src", "LedgerForge.Web", "Views", "Shared", "_Layout.cshtml");
        var routes = new[]
        {
            "/admin/finance", "/admin/lookups", "/admin/organization",
            "/admin/security", "/admin/diagnostics", "/imports", "/work"
        };

        Assert.Contains("lf-admin-subnav", layout, StringComparison.Ordinal);
        Assert.All(routes, route => Assert.Contains($"href=\"{route}\"", layout, StringComparison.Ordinal));
    }

    [Fact]
    public void DashboardFilters_AreBackedByServerSideDimensionScoping()
    {
        var home = Read("src", "LedgerForge.Web", "Views", "Home", "Index.cshtml");
        var service = Read("src", "LedgerForge.Infrastructure", "Dashboard", "DashboardService.cs");

        Assert.Contains("name=\"fiscalYearId\"", home, StringComparison.Ordinal);
        Assert.Contains("name=\"locationId\"", home, StringComparison.Ordinal);
        Assert.Contains("name=\"categoryId\"", home, StringComparison.Ordinal);
        Assert.Contains("GetScopedCommitmentAsync", service, StringComparison.Ordinal);
        Assert.Contains("GetScopedPublishedForecastAsync", service, StringComparison.Ordinal);
        Assert.Contains("x.LocationId == selectedLocationId.Value", service, StringComparison.Ordinal);
        Assert.Contains("x.InternalCategoryId == selectedCategoryId.Value", service, StringComparison.Ordinal);
    }

    [Fact]
    public void BudgetWorkspace_ExposesReferenceStyleFiltersDrawerAndDimensions()
    {
        var budget = Read("src", "LedgerForge.Web", "Views", "Budget", "Index.cshtml");

        Assert.Contains("lf-budget-filters", budget, StringComparison.Ordinal);
        Assert.Contains("name=\"section\"", budget, StringComparison.Ordinal);
        Assert.Contains("name=\"category\"", budget, StringComparison.Ordinal);
        Assert.Contains("name=\"location\"", budget, StringComparison.Ordinal);
        Assert.Contains("name=\"vendor\"", budget, StringComparison.Ordinal);
        Assert.Contains("name=\"need\"", budget, StringComparison.Ordinal);
        Assert.Contains("lf-budget-drawer", budget, StringComparison.Ordinal);
        Assert.Contains("Planned Total", budget, StringComparison.Ordinal);
        Assert.Contains("Must Have", budget, StringComparison.Ordinal);
    }

    [Fact]
    public void BudgetItemWorkspace_UsesRealFinancialAndRelatedRecordSurfaces()
    {
        var item = Read("src", "LedgerForge.Web", "Views", "Budget", "Item.cshtml");
        var service = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetItemWorkspaceService.cs");

        Assert.Contains("Purchase Orders", item, StringComparison.Ordinal);
        Assert.Contains("Actuals &amp; Invoices", item, StringComparison.Ordinal);
        Assert.Contains("Financial Breakdown", item, StringComparison.Ordinal);
        Assert.Contains("Recent Activity", item, StringComparison.Ordinal);
        Assert.Contains("PurchaseOrderLines", service, StringComparison.Ordinal);
        Assert.Contains("InvoiceAllocations", service, StringComparison.Ordinal);
        Assert.Contains("ActualTransactions", service, StringComparison.Ordinal);
        Assert.Contains("ForecastLines", service, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferenceWorkspaceStyles_AreExternalForStrictCsp()
    {
        var layout = Read("src", "LedgerForge.Web", "Views", "Shared", "_Layout.cshtml");

        Assert.Contains("ledgerforge-workspace.css", layout, StringComparison.Ordinal);
        Assert.Contains("ledgerforge-item.css", layout, StringComparison.Ordinal);
        Assert.Contains("ledgerforge-adminnav.css", layout, StringComparison.Ordinal);
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
