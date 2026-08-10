using LedgerForge.Infrastructure.Actuals;
using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Web.Documents;

namespace LedgerForge.Web.Models.Budgeting;

public sealed record BudgetItemEditViewModel(
    BudgetItemWorkspaceSnapshot Workspace,
    IReadOnlyList<StoredDocument> Documents,
    bool CanEdit,
    bool CanPostActuals,
    BudgetItemActualEntryOptions ActualEntryOptions,
    string? ErrorMessage = null,
    bool Saved = false)
{
    public BudgetItemEditSnapshot Item => Workspace.Item;
}
