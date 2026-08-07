using LedgerForge.Domain.MasterData;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class FinanceAccountTests
{
    [Fact]
    public void FinanceCategory_CanBeAssignedAndCleared()
    {
        var categoryId = Guid.NewGuid();
        var account = new FinanceAccount("6100", "Software", categoryId);

        Assert.Equal(categoryId, account.FinanceCategoryId);

        account.SetFinanceCategory(null);

        Assert.Null(account.FinanceCategoryId);
    }

    [Fact]
    public void FinanceCategory_RejectsEmptyGuid()
    {
        var account = new FinanceAccount("6100", "Software");

        Assert.Throws<ArgumentException>(() => account.SetFinanceCategory(Guid.Empty));
    }
}
