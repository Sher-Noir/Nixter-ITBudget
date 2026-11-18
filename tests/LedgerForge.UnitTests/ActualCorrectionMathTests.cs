using LedgerForge.Domain.Actuals;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class ActualCorrectionMathTests
{
    [Fact]
    public void Correction_RetainsOriginalAndReversalWhileReplacingNetSpend()
    {
        var year = Guid.NewGuid();
        var item = Guid.NewGuid();
        var original = new ActualTransaction(year, new DateOnly(2038, 1, 1), 142.99m,
            "Original", budgetItemId: item);
        var reversal = original.CreateReversal(new DateOnly(2038, 1, 1), "Correct charge");
        var replacement = new ActualTransaction(year, new DateOnly(2038, 1, 2), 183.45m,
            "Replacement", budgetItemId: item);

        Assert.Equal(original.Id, reversal.ReversesTransactionId);
        Assert.Equal("Correct charge", reversal.ReversalReason);
        Assert.Equal(-original.Amount, reversal.Amount);
        Assert.Equal(183.45m, original.Amount + reversal.Amount + replacement.Amount);
        Assert.Equal(item, reversal.BudgetItemId);
        Assert.Equal(item, replacement.BudgetItemId);
    }

    [Fact]
    public void Reversal_RequiresCorrectionReason()
    {
        var original = new ActualTransaction(Guid.NewGuid(), new DateOnly(2038, 1, 1), 10m, "Charge");
        Assert.Throws<ArgumentException>(() => original.CreateReversal(new DateOnly(2038, 1, 1), " "));
    }
}
