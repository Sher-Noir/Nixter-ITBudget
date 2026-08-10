namespace LedgerForge.Web.Security;

public enum ModuleAccessLevel
{
    None = 0,
    View = 10,
    Edit = 20,
    Manage = 30,
    Admin = 40
}

public enum LedgerForgeModule
{
    Dashboard,
    Budget,
    Procurement,
    Vendors,
    Contracts,
    Renewals,
    Documents,
    Approvals,
    Reports,
    FiscalYears,
    Imports,
    Audit,
    Administration
}

public enum SecurityPrincipalType
{
    User,
    ActiveDirectoryGroup
}

public static class ModuleAccess
{
    public static bool Meets(ModuleAccessLevel actual, ModuleAccessLevel required)
        => actual >= required;

    public static ModuleAccessLevel Highest(ModuleAccessLevel left, ModuleAccessLevel right)
        => left >= right ? left : right;
}
