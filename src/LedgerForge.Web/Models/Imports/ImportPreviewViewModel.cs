using LedgerForge.Infrastructure.Importing;

namespace LedgerForge.Web.Models.Imports;

public sealed record ImportIndexViewModel(
    IReadOnlyList<ImportBatchSummary> Batches,
    string? ErrorMessage = null);

public sealed record ImportBatchDetailViewModel(
    ImportBatchDetail Detail,
    string? ErrorMessage = null,
    bool Saved = false,
    bool Accepted = false);
