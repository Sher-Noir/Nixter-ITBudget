using Crch.ItBudget.Domain.Security;
using Crch.ItBudget.Infrastructure.Security;

namespace Crch.ItBudget.Web.Models.Administration;

public sealed record SecurityMappingsViewModel(
    IReadOnlyList<AdGroupMappingSummary> Mappings,
    IReadOnlyList<UserRoleExceptionSummary> UserExceptions,
    IReadOnlyList<ApplicationRole> Roles,
    IReadOnlyList<UserRoleExceptionEffect> Effects,
    string? ErrorMessage = null,
    string? SuccessMessage = null);
