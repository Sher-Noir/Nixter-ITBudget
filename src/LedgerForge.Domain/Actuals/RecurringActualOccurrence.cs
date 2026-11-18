using LedgerForge.Domain.Common;

namespace LedgerForge.Domain.Actuals;

/// <summary>Immutable billing-month posting marker; unique per schedule/month.</summary>
public sealed class RecurringActualOccurrence : AuditableEntity
{
    private RecurringActualOccurrence() { }

    public RecurringActualOccurrence(Guid scheduleId, Guid actualTransactionId,
        int billingYear, int billingMonth, DateOnly billingDate)
    {
        if (scheduleId == Guid.Empty || actualTransactionId == Guid.Empty)
            throw new ArgumentException("Schedule and transaction IDs are required.");
        if (billingYear is < 2000 or > 9999 || billingMonth is < 1 or > 12 ||
            billingDate.Year != billingYear || billingDate.Month != billingMonth)
            throw new ArgumentException("Invalid billing period.");
        ScheduleId = scheduleId;
        ActualTransactionId = actualTransactionId;
        BillingYear = billingYear;
        BillingMonth = billingMonth;
        BillingDate = billingDate;
    }

    public Guid ScheduleId { get; private set; }
    public Guid ActualTransactionId { get; private set; }
    public int BillingYear { get; private set; }
    public int BillingMonth { get; private set; }
    public DateOnly BillingDate { get; private set; }
}
