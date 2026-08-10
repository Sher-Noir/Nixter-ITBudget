using LedgerForge.Domain.Security;
using Microsoft.AspNetCore.Authorization;

namespace LedgerForge.Web.Security;

public static class AuthorizationPolicies
{
    public const string AuthenticationOnly = nameof(AuthenticationOnly);
    public const string ViewBudget = nameof(ViewBudget);
    public const string EditPlanningBudget = nameof(EditPlanningBudget);
    public const string ManageBudget = nameof(ManageBudget);
    public const string PostActuals = nameof(PostActuals);
    public const string ManageProcurement = nameof(ManageProcurement);
    public const string ManageVendors = nameof(ManageVendors);
    public const string ManageContracts = nameof(ManageContracts);
    public const string EditDocuments = nameof(EditDocuments);
    public const string Approve = nameof(Approve);
    public const string ManageImports = nameof(ManageImports);
    public const string ManageFiscalYears = nameof(ManageFiscalYears);
    public const string ViewAudit = nameof(ViewAudit);
    public const string Administration = nameof(Administration);

    public static string ModulePolicy(LedgerForgeModule module, ModuleAccessLevel minimum)
        => $"Module:{module}:{minimum}";

    public static void Configure(AuthorizationOptions options)
    {
        var anyApplicationRole = new ApplicationRoleRequirement(Enum.GetValues<ApplicationRole>());
        options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(anyApplicationRole).Build();
        options.FallbackPolicy = options.DefaultPolicy;
        options.AddPolicy(AuthenticationOnly, policy => policy.RequireAuthenticatedUser());

        // ViewBudget is retained as the broad authenticated-workspace compatibility policy.
        // Per-module view enforcement is added centrally by ModuleAccessMiddleware.
        Add(options, ViewBudget, null, ModuleAccessLevel.View,
            ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.BudgetEditor,
            ApplicationRole.Approver, ApplicationRole.ReadOnly, ApplicationRole.Auditor);
        Add(options, EditPlanningBudget, LedgerForgeModule.Budget, ModuleAccessLevel.Edit,
            ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.BudgetEditor);
        Add(options, ManageBudget, LedgerForgeModule.Budget, ModuleAccessLevel.Manage,
            ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator);
        Add(options, PostActuals, LedgerForgeModule.Budget, ModuleAccessLevel.Edit,
            ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.BudgetEditor);
        Add(options, ManageProcurement, LedgerForgeModule.Procurement, ModuleAccessLevel.Manage,
            ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.BudgetEditor);
        Add(options, ManageVendors, LedgerForgeModule.Vendors, ModuleAccessLevel.Manage,
            ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.BudgetEditor);
        Add(options, ManageContracts, LedgerForgeModule.Contracts, ModuleAccessLevel.Manage,
            ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.BudgetEditor);
        Add(options, EditDocuments, LedgerForgeModule.Documents, ModuleAccessLevel.Edit,
            ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.BudgetEditor);
        Add(options, Approve, LedgerForgeModule.Approvals, ModuleAccessLevel.Manage,
            ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.Approver);
        Add(options, ManageImports, LedgerForgeModule.Imports, ModuleAccessLevel.Manage,
            ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator);
        Add(options, ManageFiscalYears, LedgerForgeModule.FiscalYears, ModuleAccessLevel.Manage,
            ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator);
        Add(options, ViewAudit, LedgerForgeModule.Audit, ModuleAccessLevel.View,
            ApplicationRole.SystemAdministrator, ApplicationRole.Auditor);
        Add(options, Administration, LedgerForgeModule.Administration, ModuleAccessLevel.Admin,
            ApplicationRole.SystemAdministrator);

        foreach (var module in Enum.GetValues<LedgerForgeModule>())
        {
            foreach (var level in Enum.GetValues<ModuleAccessLevel>().Where(x => x != ModuleAccessLevel.None))
                Add(options, ModulePolicy(module, level), module, level, LegacyRolesFor(module, level));
        }
    }

    private static void Add(
        AuthorizationOptions options,
        string policyName,
        LedgerForgeModule? module,
        ModuleAccessLevel minimumAccess,
        params ApplicationRole[] roles)
    {
        options.AddPolicy(policyName, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new ApplicationRoleRequirement(module, minimumAccess, roles));
        });
    }

    private static ApplicationRole[] LegacyRolesFor(LedgerForgeModule module, ModuleAccessLevel minimum)
    {
        if (minimum == ModuleAccessLevel.Admin) return [ApplicationRole.SystemAdministrator];
        if (minimum == ModuleAccessLevel.View)
        {
            return module switch
            {
                LedgerForgeModule.Administration => [ApplicationRole.SystemAdministrator],
                LedgerForgeModule.Audit => [ApplicationRole.SystemAdministrator, ApplicationRole.Auditor],
                LedgerForgeModule.Imports or LedgerForgeModule.FiscalYears => [ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator],
                LedgerForgeModule.Approvals => [ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.Approver],
                _ => [ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.BudgetEditor, ApplicationRole.Approver, ApplicationRole.ReadOnly, ApplicationRole.Auditor]
            };
        }

        return module switch
        {
            LedgerForgeModule.Budget => minimum <= ModuleAccessLevel.Edit
                ? [ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.BudgetEditor]
                : [ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator],
            LedgerForgeModule.Procurement or LedgerForgeModule.Vendors or LedgerForgeModule.Contracts or LedgerForgeModule.Renewals
                => [ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.BudgetEditor],
            LedgerForgeModule.Documents => minimum <= ModuleAccessLevel.Edit
                ? [ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.BudgetEditor]
                : [ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator],
            LedgerForgeModule.Approvals => [ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.Approver],
            LedgerForgeModule.Reports => [ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator],
            LedgerForgeModule.FiscalYears or LedgerForgeModule.Imports => [ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator],
            LedgerForgeModule.Audit => [ApplicationRole.SystemAdministrator, ApplicationRole.Auditor],
            LedgerForgeModule.Administration => [ApplicationRole.SystemAdministrator],
            LedgerForgeModule.Dashboard => [ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.BudgetEditor],
            _ => [ApplicationRole.SystemAdministrator]
        };
    }
}

public sealed class ApplicationRoleRequirement : IAuthorizationRequirement
{
    public ApplicationRoleRequirement(params ApplicationRole[] allowedRoles)
        : this(null, ModuleAccessLevel.View, allowedRoles) { }

    public ApplicationRoleRequirement(
        LedgerForgeModule? module,
        ModuleAccessLevel minimumAccess,
        params ApplicationRole[] allowedRoles)
    {
        Module = module;
        MinimumAccess = minimumAccess;
        AllowedRoles = allowedRoles;
    }

    public LedgerForgeModule? Module { get; }
    public ModuleAccessLevel MinimumAccess { get; }
    public IReadOnlyCollection<ApplicationRole> AllowedRoles { get; }
}