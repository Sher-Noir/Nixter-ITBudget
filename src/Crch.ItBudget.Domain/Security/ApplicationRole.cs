namespace Crch.ItBudget.Domain.Security;

public enum ApplicationRole
{
    SystemAdministrator,
    BudgetAdministrator,
    BudgetEditor,
    Approver,
    ReadOnly,
    Auditor
}
