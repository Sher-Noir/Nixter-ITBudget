using LedgerForge.Domain.Actuals;
using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Actuals;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LedgerForge.IntegrationTests;

public sealed class BudgetItemActualCorrectionTests
{
    // Only run against the disposable SQL Server supplied by the CI migration gate.
    [Fact]
    public async Task CorrectManualActual_IsAtomicAuditableAndIdempotent_WhenSqlGateIsAvailable()
    {
        var connectionString = Environment.GetEnvironmentVariable("LEDGERFORGE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<LedgerForgeDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var db = new LedgerForgeDbContext(options);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var year = new FiscalYear($"Correction {suffix}", new DateOnly(2038, 1, 1), new DateOnly(2038, 12, 31), 2038);
        year.SetStatus(FiscalYearStatus.Active);
        var version = new BudgetVersion(year.Id, $"Correction {suffix}", BudgetVersionType.InitialPlanning, 1, effectiveDate: year.StartDate);
        var item = new BudgetItem(year.Id, version.Id, $"ITEM-{suffix}", "1", "Correction test item", 1m, 1000m);
        db.FiscalYears.Add(year);
        db.BudgetVersions.Add(version);
        db.BudgetItems.Add(item);
        await db.SaveChangesAsync();

        var service = new ActualLedgerService(db);
        var id = await service.PostManualAsync(
            year.Id, new DateOnly(2038, 6, 1), 142.99m, "Original",
            "CARD-ORIGINAL", item.Id, null, null, null, null);

        var corrected = await service.CorrectBudgetItemManualAsync(
            item.Id, id, new DateOnly(2038, 6, 5), 183.45m, "Corrected",
            "CARD-CORRECTED", null, null, null, "Incorrect charge amount");

        db.ChangeTracker.Clear();
        var rows = await db.ActualTransactions.AsNoTracking()
            .Where(x => x.FiscalYearId == year.Id).ToListAsync();

        Assert.Equal(3, rows.Count);
        Assert.Equal(183.45m, rows.Sum(x => x.Amount));
        Assert.Equal(816.55m, 1000m - rows.Sum(x => x.Amount));
        var original = Assert.Single(rows, x => x.Id == id);
        var reversal = Assert.Single(rows, x => x.Kind == ActualTransactionKind.Reversal);
        var replacement = Assert.Single(rows, x => x.Id == corrected);
        Assert.Equal(-142.99m, reversal.Amount);
        Assert.Equal(original.Id, reversal.ReversesTransactionId);
        Assert.Equal("Incorrect charge amount", reversal.ReversalReason);
        Assert.Equal("Corrected", replacement.Description);
        Assert.Equal(new DateOnly(2038, 6, 5), replacement.TransactionDate);
        Assert.Equal(item.Id, replacement.BudgetItemId);
        Assert.Equal(142.99m, original.Amount);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CorrectBudgetItemManualAsync(item.Id, id, new DateOnly(2038, 6, 5),
                1m, "Duplicate", null, null, null, null, "Duplicate correction"));
        db.ChangeTracker.Clear();
        Assert.Equal(3, await db.ActualTransactions.CountAsync(x => x.FiscalYearId == year.Id));

        var otherItem = new BudgetItem(year.Id, version.Id, $"OTHER-{suffix}", "2", "Other item", 1m, 100m);
        db.BudgetItems.Add(otherItem);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CorrectBudgetItemManualAsync(otherItem.Id, corrected,
                new DateOnly(2038, 6, 6), 10m, "Wrong item", null, null, null, null, "Wrong item"));

        var imported = new ActualTransaction(year.Id, new DateOnly(2038, 7, 1), 20m,
            "Imported item", ActualTransactionKind.Import, budgetItemId: item.Id);
        db.ActualTransactions.Add(imported);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CorrectBudgetItemManualAsync(item.Id, imported.Id,
                new DateOnly(2038, 7, 2), 25m, "Not allowed", null, null, null, null, "Cannot edit import"));

        db.ChangeTracker.Clear();
        var trackedYear = await db.FiscalYears.SingleAsync(x => x.Id == year.Id);
        trackedYear.Close(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CorrectBudgetItemManualAsync(item.Id, corrected,
                new DateOnly(2038, 6, 7), 200m, "Closed", null, null, null, null, "Closed fiscal year"));
    }
}
