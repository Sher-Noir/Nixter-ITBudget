using Crch.ItBudget.Domain.Security;
using Crch.ItBudget.Infrastructure.Security;

namespace Crch.ItBudget.Web.Models.Administration;

public sealed record SecurityMappingsViewModel(
    IReadOnlyList<AdGroupMappingSummary> Mappings,
    IReadOnlyList<ApplicationRole> Roles,
    string? ErrorMessage = null,
    string? SuccessMessage = null);
