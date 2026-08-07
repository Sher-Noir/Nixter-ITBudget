using System.Text;
using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Actuals;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LedgerForge.IntegrationTests;

public sealed class ActualCsvImportIntegrationTests
{
    [Fact]
    public async Task Import_IsAtomicAndPersistsValidatedRows_WhenSqlGateIsAvailable()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__LedgerForge");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var options = new DbContextOptionsBuilder<LedgerForgeDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var dbContext = new LedgerForgeDbContext(options);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var year = new FiscalYear($"CSV {suffix}", new DateOnly(2035, 1, 1), new DateOnly(2035, 12, 31), 2035);
        year.SetStatus(FiscalYearStatus.Active);
        dbContext.FiscalYears.Add(year);
        dbContext.BudgetVersions.Add(new BudgetVersion(year.Id, $"CSV {suffix}", BudgetVersionType.InitialPlanning, 1, effectiveDate: year.StartDate));
        await dbContext.SaveChangesAsync();

        var service = new ActualCsvImportService(dbContext);
        var validCsv = "TransactionDate,Amount,Description,SourceReference\r\n2035-02-01,125.25,First row,CSV-1\r\n2035-03-01,74.75,Second row,CSV-2\r\n";
        await using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(validCsv)))
        {
            var result = await service.ImportAsync(year.Id, stream, ActualImportProfile.Default, "valid.csv");
            Assert.True(result.Succeeded, string.Join(" | ", result.Errors));
            Assert.Equal(2, result.ImportedRows);
            Assert.Equal(200m, result.ImportedTotal);
        }

        Assert.Equal(2, await dbContext.ActualTransactions.CountAsync(x => x.FiscalYearId == year.Id));

        var invalidCsv = "TransactionDate,Amount,Description\r\n2035-04-01,10.00,Would otherwise be valid\r\nnot-a-date,20.00,Bad row\r\n";
        await using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(invalidCsv)))
        {
            var result = await service.ImportAsync(year.Id, stream, ActualImportProfile.Default, "invalid.csv");
            Assert.False(result.Succeeded);
            Assert.Equal(0, result.ImportedRows);
        }

        Assert.Equal(2, await dbContext.ActualTransactions.CountAsync(x => x.FiscalYearId == year.Id));
    }
}
