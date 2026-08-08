using LedgerForge.Infrastructure.Budgeting;

namespace LedgerForge.Web.Models.Budgeting;

public sealed record BudgetPlanningViewModel(
    BudgetPlanningSnapshot Snapshot,
    bool CanEdit,
    string? ErrorMessage = null,
    bool Saved = false);
