using LedgerForge.Infrastructure.Budgeting;

namespace LedgerForge.Web.Models.FiscalYears;

public sealed record FiscalYearIndexViewModel(
    IReadOnlyList<FiscalYearSummary> FiscalYears,
    string? ErrorMessage = null,
    bool Saved = false);

public sealed record FiscalYearDetailViewModel(
    FiscalYearSummary FiscalYear,
    IReadOnlyList<BudgetVersionSummary> Versions,
    FiscalCloseReadiness CloseReadiness);
