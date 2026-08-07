using LedgerForge.Infrastructure.Importing;

namespace LedgerForge.Web.Models.Imports;

public sealed record ImportPreviewViewModel(
    LegacyBudgetImportPreviewResult? Result = null,
    string? ErrorMessage = null);
