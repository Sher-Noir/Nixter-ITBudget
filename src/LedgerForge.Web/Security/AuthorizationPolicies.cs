using LedgerForge.Domain.Security;
using Microsoft.AspNetCore.Authorization;

namespace LedgerForge.Web.Security;

public static class AuthorizationPolicies
{
    public const string ViewBudget = nameof(ViewBudget);
    public const string EditPlanningBudget = nameof(EditPlanningBudget);
    public const string ManageBudget = nameof(ManageBudget);
    public const string PostActuals = nameof(PostActuals);
    public const string ManageProcurement = nameof(ManageProcurement);
    public const string Approve = nameof(Approve);
    public const string ManageImports = nameof(ManageImports);
    public const string ViewAudit = nameof(ViewAudit);
    public const string Administration = nameof(Administration);

    public static void Configure(AuthorizationOptions options)
    {
        var anyApplicationRole = new ApplicationRoleRequirement(Enum.GetValues<ApplicationRole>());
        options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(anyApplicationRole).Build();
        options.FallbackPolicy = options.DefaultPolicy;

        Add(options, ViewBudget, ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.BudgetEditor, ApplicationRole.Approver, ApplicationRole.ReadOnly, ApplicationRole.Auditor);
        Add(options, EditPlanningBudget, ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.BudgetEditor);
        Add(options, ManageBudget, ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator);
        Add(options, PostActuals, ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.BudgetEditor);
        Add(options, ManageProcurement, ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.BudgetEditor);
        Add(options, Approve, ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator, ApplicationRole.Approver);
        Add(options, ManageImports, ApplicationRole.SystemAdministrator, ApplicationRole.BudgetAdministrator);
        Add(options, ViewAudit, ApplicationRole.SystemAdministrator, ApplicationRole.Auditor);
        Add(options, Administration, ApplicationRole.SystemAdministrator);
    }

    private static void Add(AuthorizationOptions options, string policyName, params ApplicationRole[] roles)
    {
        options.AddPolicy(policyName, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new ApplicationRoleRequirement(roles));
        });
    }
}

public sealed class ApplicationRoleRequirement(params ApplicationRole[] allowedRoles) : IAuthorizationRequirement
{
    public IReadOnlyCollection<ApplicationRole> AllowedRoles { get; } = allowedRoles;
}
