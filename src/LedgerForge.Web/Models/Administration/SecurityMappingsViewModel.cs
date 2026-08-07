using LedgerForge.Domain.Security;
using LedgerForge.Infrastructure.Security;

namespace LedgerForge.Web.Models.Administration;

public sealed record SecurityMappingsViewModel(
    IReadOnlyList<AdGroupMappingSummary> Mappings,
    IReadOnlyList<UserRoleExceptionSummary> UserExceptions,
    IReadOnlyList<ApplicationRole> Roles,
    IReadOnlyList<UserRoleExceptionEffect> Effects,
    string? ErrorMessage = null,
    string? SuccessMessage = null);
