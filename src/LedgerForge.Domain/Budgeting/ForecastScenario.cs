using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Budgeting;

public enum ForecastScenarioState
{
    Draft,
    Published,
    Archived
}

public sealed class ForecastScenario : AuditableEntity
{
    private ForecastScenario() { }

    public ForecastScenario(
        Guid fiscalYearId,
        Guid budgetVersionId,
        string name,
        DateOnly asOfDate,
        string? notes = null)
    {
        if (fiscalYearId == Guid.Empty) throw new ArgumentException("Fiscal year is required.", nameof(fiscalYearId));
        if (budgetVersionId == Guid.Empty) throw new ArgumentException("Budget version is required.", nameof(budgetVersionId));
        FiscalYearId = fiscalYearId;
        BudgetVersionId = budgetVersionId;
        Name = Required(name, 150, nameof(name));
        AsOfDate = asOfDate;
        Notes = Optional(notes, 2000, nameof(notes));
        State = ForecastScenarioState.Draft;
    }

    public Guid FiscalYearId { get; private set; }
    public Guid BudgetVersionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateOnly AsOfDate { get; private set; }
    public string? Notes { get; private set; }
    public ForecastScenarioState State { get; private set; }
    public string? PublishedBy { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public string? ArchivedBy { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }
    public string? ArchiveReason { get; private set; }

    public void Publish(string actor, DateTimeOffset atUtc)
    {
        if (State != ForecastScenarioState.Draft) throw new InvalidOperationException($"Forecast cannot be published from state {State}.");
        PublishedBy = Required(actor, 256, nameof(actor));
        PublishedAtUtc = atUtc;
        State = ForecastScenarioState.Published;
    }

    public void Archive(string actor, string reason, DateTimeOffset atUtc)
    {
        if (State == ForecastScenarioState.Archived) throw new InvalidOperationException("Forecast is already archived.");
        ArchivedBy = Required(actor, 256, nameof(actor));
        ArchivedAtUtc = atUtc;
        ArchiveReason = Required(reason, 1000, nameof(reason));
        State = ForecastScenarioState.Archived;
    }

    private static string Required(string value, int max, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters.", parameterName);
        return normalized;
    }

    private static string? Optional(string? value, int max, string parameterName)
        => string.IsNullOrWhiteSpace(value) ? null : Required(value, max, parameterName);
}

public sealed class ForecastLine : AuditableEntity
{
    private ForecastLine() { }

    public ForecastLine(Guid forecastScenarioId, Guid budgetItemId, decimal forecastTotal, string? note = null)
    {
        if (forecastScenarioId == Guid.Empty) throw new ArgumentException("Forecast scenario is required.", nameof(forecastScenarioId));
        if (budgetItemId == Guid.Empty) throw new ArgumentException("Budget item is required.", nameof(budgetItemId));
        ValidateAmount(forecastTotal);
        ForecastScenarioId = forecastScenarioId;
        BudgetItemId = budgetItemId;
        ForecastTotal = forecastTotal;
        Note = Optional(note, 1000, nameof(note));
    }

    public Guid ForecastScenarioId { get; private set; }
    public Guid BudgetItemId { get; private set; }
    public decimal ForecastTotal { get; private set; }
    public string? Note { get; private set; }

    public void Update(decimal forecastTotal, string? note)
    {
        ValidateAmount(forecastTotal);
        ForecastTotal = forecastTotal;
        Note = Optional(note, 1000, nameof(note));
    }

    private static void ValidateAmount(decimal value)
    {
        if (value < 0m) throw new ArgumentOutOfRangeException(nameof(value), "Forecast total cannot be negative.");
    }

    private static string? Optional(string? value, int max, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > max) throw new ArgumentException($"Value cannot exceed {max} characters.", parameterName);
        return normalized;
    }
}
