using LedgerForge.Domain.Actuals;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class RecurringActualScheduleTests
{
    private static RecurringActualSchedule Hycu(DateOnly start, DateOnly end) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "HYCU monthly", 1310.40m, true, 31,
            start, end, "HYCU-MONTHLY");

    [Fact]
    public void LastDay_HandlesShortMonthsAndLeapYears()
    {
        var schedule = Hycu(new DateOnly(2028, 1, 1), new DateOnly(2028, 12, 31));
        Assert.Equal(new DateOnly(2028, 1, 31), schedule.BillingDate(2028, 1));
        Assert.Equal(new DateOnly(2028, 2, 29), schedule.BillingDate(2028, 2));
        Assert.Equal(new DateOnly(2028, 4, 30), schedule.BillingDate(2028, 4));
        Assert.Equal(new DateOnly(2027, 2, 28), schedule.BillingDate(2027, 2));
    }

    [Fact]
    public void BillingDate_NotAnActualUntilTheFollowingCalendarDay()
    {
        var schedule = Hycu(new DateOnly(2028, 1, 1), new DateOnly(2028, 3, 31));
        Assert.False(schedule.Due(2028, 1, new DateOnly(2028, 1, 30)));
        Assert.False(schedule.Due(2028, 1, new DateOnly(2028, 1, 31)));
        Assert.True(schedule.Due(2028, 1, new DateOnly(2028, 2, 1)));
        Assert.False(schedule.Due(2028, 2, new DateOnly(2028, 2, 29)));
        Assert.True(schedule.Due(2028, 2, new DateOnly(2028, 3, 1)));
    }

    [Fact]
    public void ScheduleCanBeEditedWithoutChangingPreviouslyProducedAmounts()
    {
        var schedule = Hycu(new DateOnly(2028, 1, 1), new DateOnly(2028, 3, 31));
        var existingPostedAmount = schedule.Amount;
        schedule.Update("HYCU updated", 1410.40m, true, 31,
            schedule.StartDate, schedule.EndDate, "HYCU-MONTHLY", null, null, null);
        Assert.Equal(1310.40m, existingPostedAmount);
        Assert.Equal(1410.40m, schedule.Amount);
        schedule.SetActive(false);
        Assert.False(schedule.Due(2028, 2, new DateOnly(2028, 3, 1)));
        schedule.SetActive(true);
        Assert.True(schedule.Due(2028, 2, new DateOnly(2028, 3, 1)));
    }

    [Fact]
    public void FixedDay_ClampsToLastDayWhenMonthIsShort()
    {
        var schedule = new RecurringActualSchedule(Guid.NewGuid(), Guid.NewGuid(),
            "Monthly service", 10m, false, 30,
            new DateOnly(2028, 1, 1), new DateOnly(2028, 3, 31));
        Assert.Equal(new DateOnly(2028, 2, 29), schedule.BillingDate(2028, 2));
    }
}
