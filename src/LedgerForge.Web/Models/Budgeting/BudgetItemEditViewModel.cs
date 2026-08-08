using LedgerForge.Infrastructure.Budgeting;

namespace LedgerForge.Web.Models.Budgeting;

public sealed record BudgetItemEditViewModel(
    BudgetItemEditSnapshot Item,
    bool CanEdit,
    string? ErrorMessage = null,
    bool Saved = false);
