using LedgerForge.Infrastructure.Budgeting;

namespace LedgerForge.Web.Models.Budgeting;

public sealed record BudgetAmendmentViewModel(
    BudgetAmendmentSnapshot Snapshot,
    Guid? SelectedBudgetItemId,
    bool CanApprove,
    string? ErrorMessage = null,
    bool Saved = false);
