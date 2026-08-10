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
        Assert.Contains("UseMiddleware<ModuleAccessMiddleware>()", program, StringComparison.Ordinal);
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
    public void WindowsSignIn_UsesExplicitAnonymousLandingWithoutCollectingPasswords()
    {
        var account = Read("src", "LedgerForge.Web", "Controllers", "AccountController.cs");
        var login = Read("src", "LedgerForge.Web", "Views", "Account", "Login.cshtml");
        var home = Read("src", "LedgerForge.Web", "Controllers", "HomeController.cs");

        Assert.Contains("[AllowAnonymous]", account, StringComparison.Ordinal);
        Assert.Contains("AuthenticationOnly", account, StringComparison.Ordinal);
        Assert.Contains("Continue with Windows", login, StringComparison.Ordinal);
        Assert.Contains("does not collect or store your Windows password", login, StringComparison.Ordinal);
        Assert.Contains("RedirectToAction(\"Login\", \"Account\"", home, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"password\"", login, StringComparison.OrdinalIgnoreCase);
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
    public void MutableHostConfiguration_UsesWritableNonWebRootStorage()
    {
        var organization = Read("src", "LedgerForge.Web", "Configuration", "OrganizationSettingsStore.cs");
        var security = Read("src", "LedgerForge.Web", "Security", "SecurityAccessConfigurationStore.cs");

        Assert.Contains("Documents:StoragePath", organization, StringComparison.Ordinal);
        Assert.Contains(".ledgerforge", organization, StringComparison.Ordinal);
        Assert.Contains("Organization settings storage cannot be inside the public web root", organization, StringComparison.Ordinal);
        Assert.Contains("security-access.json", security, StringComparison.Ordinal);
        Assert.Contains("cannot be stored inside the public web root", security, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurableAuthorization_UsesRoleModuleAccessLevelsAndDirectoryAssignments()
    {
        var access = Read("src", "LedgerForge.Web", "Security", "ModuleAccess.cs");
        var resolver = Read("src", "LedgerForge.Web", "Security", "ModuleAccessResolver.cs");
        var policies = Read("src", "LedgerForge.Web", "Security", "AuthorizationPolicies.cs");
        var securityView = Read("src", "LedgerForge.Web", "Views", "AdministrationSecurity", "Index.cshtml");

        Assert.Contains("None = 0", access, StringComparison.Ordinal);
        Assert.Contains("View = 10", access, StringComparison.Ordinal);
        Assert.Contains("Edit = 20", access, StringComparison.Ordinal);
        Assert.Contains("Manage = 30", access, StringComparison.Ordinal);
        Assert.Contains("Admin = 40", access, StringComparison.Ordinal);
        Assert.Contains("ActiveDirectoryGroup", access, StringComparison.Ordinal);
        Assert.Contains("principal.IsInRole", resolver, StringComparison.Ordinal);
        Assert.Contains("ModulePolicy", policies, StringComparison.Ordinal);
        Assert.Contains("Role → Module → Access", securityView, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick=", securityView, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PrimaryNavigation_IsGroupedAndActualsLivesUnderBudget()
    {
        var layout = Read("src", "LedgerForge.Web", "Views", "Shared", "_Layout.cshtml");
        var routes = new[]
        {
            "/budget", "/purchase-orders", "/invoices", "/vendors",
            "/contracts", "/renewals", "/documents", "/approvals", "/reports",
            "/fiscal-years", "/admin/organization", "/profile"
        };

        Assert.All(routes, route => Assert.Contains($"href=\"{route}\"", layout, StringComparison.Ordinal));
        Assert.DoesNotContain("href=\"/actuals\"", layout, StringComparison.Ordinal);
        Assert.Contains(">Planning</div>", layout, StringComparison.Ordinal);
        Assert.Contains(">Procurement</div>", layout, StringComparison.Ordinal);
        Assert.Contains(">Workspace</div>", layout, StringComparison.Ordinal);
        Assert.Contains(">System</div>", layout, StringComparison.Ordinal);
        Assert.Contains(">Administration</span>", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void GlobalHeaderSearch_UsesCrossModuleSearchAndFiltersByModuleAccess()
    {
        var layout = Read("src", "LedgerForge.Web", "Views", "Shared", "_Layout.cshtml");
        var search = Read("src", "LedgerForge.Web", "Controllers", "SearchController.cs");

        Assert.Contains("action=\"/search\"", layout, StringComparison.Ordinal);
        Assert.Contains("name=\"q\"", layout, StringComparison.Ordinal);
        Assert.Contains("CanViewAsync(LedgerForgeModule.Budget)", search, StringComparison.Ordinal);
        Assert.Contains("CanViewAsync(LedgerForgeModule.Vendors)", search, StringComparison.Ordinal);
        Assert.Contains("CanViewAsync(LedgerForgeModule.Procurement)", search, StringComparison.Ordinal);
        Assert.Contains("CanViewAsync(LedgerForgeModule.Contracts)", search, StringComparison.Ordinal);
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
    public void BudgetWorkspace_ExposesFiltersDrawerAndWorkingNewItemControl()
    {
        var budget = Read("src", "LedgerForge.Web", "Views", "Budget", "Index.cshtml");
        var ui = Read("src", "LedgerForge.Web", "wwwroot", "js", "ledgerforge-ui.js");

        Assert.Contains("lf-budget-filters", budget, StringComparison.Ordinal);
        Assert.Contains("name=\"section\"", budget, StringComparison.Ordinal);
        Assert.Contains("name=\"category\"", budget, StringComparison.Ordinal);
        Assert.Contains("name=\"location\"", budget, StringComparison.Ordinal);
        Assert.Contains("name=\"vendor\"", budget, StringComparison.Ordinal);
        Assert.Contains("name=\"need\"", budget, StringComparison.Ordinal);
        Assert.Contains("lf-budget-drawer", budget, StringComparison.Ordinal);
        Assert.Contains("href=\"#new-budget-item\"", budget, StringComparison.Ordinal);
        Assert.Contains("target instanceof HTMLDetailsElement", ui, StringComparison.Ordinal);
        Assert.Contains("target.open = true", ui, StringComparison.Ordinal);
    }

    [Fact]
    public void BudgetItemWorkspace_ContainsActualEntryAndRelatedFinancialSurfaces()
    {
        var item = Read("src", "LedgerForge.Web", "Views", "Budget", "Item.cshtml");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "BudgetController.cs");
        var service = Read("src", "LedgerForge.Infrastructure", "Budgeting", "BudgetItemWorkspaceService.cs");

        Assert.Contains("Purchase Orders", item, StringComparison.Ordinal);
        Assert.Contains("Actuals &amp; Invoices", item, StringComparison.Ordinal);
        Assert.Contains("Add actual", item, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/budget/items/@item.Id/actuals", item, StringComparison.Ordinal);
        Assert.Contains("PostActuals", controller, StringComparison.Ordinal);
        Assert.Contains("PurchaseOrderLines", service, StringComparison.Ordinal);
        Assert.Contains("InvoiceAllocations", service, StringComparison.Ordinal);
        Assert.Contains("ActualTransactions", service, StringComparison.Ordinal);
    }

    [Fact]
    public void FiscalYearUi_NoLongerUsesFiscalPeriods()
    {
        var index = Read("src", "LedgerForge.Web", "Views", "FiscalYears", "Index.cshtml");
        var details = Read("src", "LedgerForge.Web", "Views", "FiscalYears", "Details.cshtml");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "FiscalYearsController.cs");
        var actuals = Read("src", "LedgerForge.Web", "Views", "Actuals", "Index.cshtml");
        var import = Read("src", "LedgerForge.Infrastructure", "Actuals", "ActualCsvImportService.cs");

        Assert.DoesNotContain("fiscal period", index, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fiscal period", details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fiscalPeriodId", actuals, StringComparison.Ordinal);
        Assert.Contains("createMonthlyPeriods: false", controller, StringComparison.Ordinal);
        Assert.Contains("fiscalPeriodId: null", import, StringComparison.Ordinal);
    }

    [Fact]
    public void VendorDirectory_UsesTableEditAndSafeDeleteRetireActions()
    {
        var vendor = Read("src", "LedgerForge.Web", "Views", "Vendors", "Index.cshtml");
        var controller = Read("src", "LedgerForge.Web", "Controllers", "VendorsController.cs");

        Assert.Contains("lf-vendor-table", vendor, StringComparison.Ordinal);
        Assert.Contains(">Edit</a>", vendor, StringComparison.Ordinal);
        Assert.Contains("Delete / Retire", vendor, StringComparison.Ordinal);
        Assert.Contains("ManageVendors", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferenceWorkspaceStyles_AreExternalForStrictCsp()
    {
        var layout = Read("src", "LedgerForge.Web", "Views", "Shared", "_Layout.cshtml");

        Assert.Contains("ledgerforge-workspace.css", layout, StringComparison.Ordinal);
        Assert.Contains("ledgerforge-item.css", layout, StringComparison.Ordinal);
        Assert.Contains("ledgerforge-adminnav.css", layout, StringComparison.Ordinal);
        Assert.Contains("ledgerforge-ui.js", layout, StringComparison.Ordinal);
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