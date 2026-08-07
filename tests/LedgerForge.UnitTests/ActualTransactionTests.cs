using LedgerForge.Domain.Actuals;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class ActualTransactionTests
{
    [Fact]
    public void ManualActual_RequiresPositiveAmount()
    {
        var fiscalYearId = Guid.NewGuid();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActualTransaction(fiscalYearId, new DateOnly(2027, 1, 15), 0m, "Invalid"));
    }

    [Fact]
    public void Reversal_IsNegativeAndLinksOriginal()
    {
        var fiscalYearId = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var original = new ActualTransaction(
            fiscalYearId,
            new DateOnly(2027, 1, 15),
            125.50m,
            "Software purchase",
            ActualTransactionKind.Manual,
            "CARD-123");

        var reversal = original.CreateReversal(
            new DateOnly(2027, 1, 20),
            "Duplicate source transaction",
            periodId);

        Assert.Equal(ActualTransactionKind.Reversal, reversal.Kind);
        Assert.Equal(-125.50m, reversal.Amount);
        Assert.Equal(original.Id, reversal.ReversesTransactionId);
        Assert.Equal(periodId, reversal.FiscalPeriodId);
        Assert.Equal("Duplicate source transaction", reversal.ReversalReason);
    }

    [Fact]
    public void Reversal_CannotBeReversedAgain()
    {
        var original = new ActualTransaction(
            Guid.NewGuid(),
            new DateOnly(2027, 1, 15),
            10m,
            "Charge");
        var reversal = original.CreateReversal(
            new DateOnly(2027, 1, 16),
            "Correction");

        Assert.Throws<InvalidOperationException>(() =>
            reversal.CreateReversal(new DateOnly(2027, 1, 17), "Not allowed"));
    }
}
