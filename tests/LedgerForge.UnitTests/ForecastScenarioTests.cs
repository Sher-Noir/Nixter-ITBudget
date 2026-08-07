using LedgerForge.Domain.Budgeting;
using Xunit;

namespace LedgerForge.UnitTests;

public sealed class ForecastScenarioTests
{
    [Fact]
    public void Scenario_PublishesFromDraft()
    {
        var scenario = NewScenario();
        var now = DateTimeOffset.UtcNow;

        scenario.Publish("DOMAIN\\budget.manager", now);

        Assert.Equal(ForecastScenarioState.Published, scenario.State);
        Assert.Equal("DOMAIN\\budget.manager", scenario.PublishedBy);
        Assert.Equal(now, scenario.PublishedAtUtc);
    }

    [Fact]
    public void PublishedScenario_CannotBePublishedTwice()
    {
        var scenario = NewScenario();
        scenario.Publish("DOMAIN\\budget.manager", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            scenario.Publish("DOMAIN\\budget.manager", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Scenario_CanBeArchivedWithReason()
    {
        var scenario = NewScenario();
        scenario.Publish("DOMAIN\\budget.manager", DateTimeOffset.UtcNow);

        scenario.Archive("DOMAIN\\budget.manager", "Superseded by month-end forecast", DateTimeOffset.UtcNow.AddDays(1));

        Assert.Equal(ForecastScenarioState.Archived, scenario.State);
        Assert.Equal("Superseded by month-end forecast", scenario.ArchiveReason);
    }

    [Fact]
    public void ForecastLine_RejectsNegativeAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ForecastLine(Guid.NewGuid(), Guid.NewGuid(), -1m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ForecastLine(Guid.NewGuid(), Guid.NewGuid(), -1m, 1m));
    }

    [Fact]
    public void ForecastLine_PreservesBaselineWhenForecastChanges()
    {
        var line = new ForecastLine(Guid.NewGuid(), Guid.NewGuid(), 1000m, 1100m);

        line.Update(1250m, "Expected rate increase");

        Assert.Equal(1000m, line.BaselineTotal);
        Assert.Equal(1250m, line.ForecastTotal);
        Assert.Equal("Expected rate increase", line.Note);
    }

    [Fact]
    public void LegacyForecastLineConstructor_UsesInitialForecastAsBaseline()
    {
        var line = new ForecastLine(Guid.NewGuid(), Guid.NewGuid(), 1000m);

        Assert.Equal(1000m, line.BaselineTotal);
        Assert.Equal(1000m, line.ForecastTotal);
    }

    private static ForecastScenario NewScenario()
        => new(Guid.NewGuid(), Guid.NewGuid(), "Q2 forecast", new DateOnly(2027, 6, 30));
}
