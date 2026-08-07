using Crch.ItBudget.Infrastructure.Importing;

namespace Crch.ItBudget.Web.Models.Imports;

public sealed record ImportPreviewViewModel(
    Fy2027ImportPreviewResult? Result = null,
    string? ErrorMessage = null);
