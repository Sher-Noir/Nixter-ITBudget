using Crch.ItBudget.Domain.Common;

namespace Crch.ItBudget.Domain.Budgeting;

public sealed class FiscalPeriod : AuditableEntity
{
    private FiscalPeriod() { }

    public FiscalPeriod(
        Guid fiscalYearId,
        int periodNumber,
        string code,
        string name,
        DateOnly startDate,
        DateOnly endDate)
    {
        if (fiscalYearId == Guid.Empty) throw new ArgumentException("Fiscal year is required.", nameof(fiscalYearId));
        if (periodNumber < 1 || periodNumber > 53) throw new ArgumentOutOfRangeException(nameof(periodNumber));
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Period code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Period name is required.", nameof(name));
        if (endDate < startDate) throw new ArgumentOutOfRangeException(nameof(endDate), "End date must not precede start date.");

        FiscalYearId = fiscalYearId;
        PeriodNumber = periodNumber;
        Code = code.Trim();
        Name = name.Trim();
        StartDate = startDate;
        EndDate = endDate;
    }

    public Guid FiscalYearId { get; private set; }
    public int PeriodNumber { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsClosed { get; private set; }
    public string? ClosedBy { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public void Close(string actor, DateTimeOffset closedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Actor is required.", nameof(actor));
        if (IsClosed) return;

        IsClosed = true;
        ClosedBy = actor.Trim();
        ClosedAtUtc = closedAtUtc;
    }
}
