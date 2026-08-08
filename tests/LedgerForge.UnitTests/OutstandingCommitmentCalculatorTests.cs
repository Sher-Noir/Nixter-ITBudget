using LedgerForge.Application.Procurement;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class OutstandingCommitmentCalculatorTests
{
    [Theory]
    [InlineData(1000, 0, 1000)]
    [InlineData(1000, 250, 750)]
    [InlineData(1000, 1000, 0)]
    [InlineData(1000, 1250, 0)]
    [InlineData(0, 0, 0)]
    public void Calculate_ReturnsOpenIssuedCommitment(decimal issuedTotal, decimal postedInvoices, decimal expected)
        => Assert.Equal(expected, OutstandingCommitmentCalculator.Calculate(issuedTotal, postedInvoices));

    [Fact]
    public void Calculate_RejectsNegativeInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OutstandingCommitmentCalculator.Calculate(-1m, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => OutstandingCommitmentCalculator.Calculate(1m, -1m));
    }
}
