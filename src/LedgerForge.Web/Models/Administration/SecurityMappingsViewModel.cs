using LedgerForge.Domain.Security;
using LedgerForge.Infrastructure.Security;
using LedgerForge.Web.Security;

namespace LedgerForge.Web.Models.Administration;

public sealed record SecurityMappingsViewModel(
    IReadOnlyList<SecurityRoleDefinition> ConfigurableRoles,
    IReadOnlyList<SecurityRoleAssignment> Assignments,
    IReadOnlyList<LedgerForgeModule> Modules,
    IReadOnlyList<ModuleAccessLevel> AccessLevels,
    IReadOnlyList<AdGroupMappingSummary> Mappings,
    IReadOnlyList<UserRoleExceptionSummary> UserExceptions,
    IReadOnlyList<ApplicationRole> Roles,
    IReadOnlyList<UserRoleExceptionEffect> Effects,
    string? ErrorMessage = null,
    string? SuccessMessage = null);
