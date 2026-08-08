using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Infrastructure.Procurement;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LedgerForge.IntegrationTests;

public sealed class BudgetPlanningIntegrationTests
{
    [Fact]
    public async Task QuickAddService_PersistsBudgetItem_WhenSqlGateIsAvailable()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__LedgerForge");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<LedgerForgeDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var dbContext = new LedgerForgeDbContext(options);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var year = new FiscalYear($"Budget {suffix}", new DateOnly(2036, 1, 1), new DateOnly(2036, 12, 31), 2036);
        year.SetStatus(FiscalYearStatus.Active);
        var version = new BudgetVersion(year.Id, $"Planning {suffix}", BudgetVersionType.InitialPlanning, 1, effectiveDate: year.StartDate);
        dbContext.FiscalYears.Add(year);
        dbContext.BudgetVersions.Add(version);
        await dbContext.SaveChangesAsync();

        var fiscalYears = new FiscalYearAdministrationService(dbContext, new OutstandingCommitmentService(dbContext));
        var service = new BudgetPlanningService(dbContext, fiscalYears);

        var id = await service.AddItemAsync(year.Id, version.Id, $"ITEM-{suffix}", "Field-test budget item", 2m, 125.50m);

        var item = await dbContext.BudgetItems.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(251.00m, item.PlannedTotal);
        Assert.Equal(BudgetItemStatus.Draft, item.Status);
    }
}
