using LedgerForge.Infrastructure.Budgeting;

namespace LedgerForge.Web.Models.Budgeting;

public sealed record BudgetItemEditViewModel(
    BudgetItemWorkspaceSnapshot Workspace,
    bool CanEdit,
    string? ErrorMessage = null,
    bool Saved = false)
{
    public BudgetItemEditSnapshot Item => Workspace.Item;
}
