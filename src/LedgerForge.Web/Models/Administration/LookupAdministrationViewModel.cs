using LedgerForge.Infrastructure.MasterData;

namespace LedgerForge.Web.Models.Administration;

public sealed record LookupAdministrationViewModel(
    LookupAdministrationSnapshot Snapshot,
    string? ErrorMessage = null,
    bool Saved = false,
    bool Initialized = false);
