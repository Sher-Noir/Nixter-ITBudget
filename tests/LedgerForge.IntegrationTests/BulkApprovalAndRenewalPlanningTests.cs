using LedgerForge.Domain.Budgeting;
using LedgerForge.Infrastructure.Approvals;
using LedgerForge.Infrastructure.Budgeting;
using LedgerForge.Infrastructure.Persistence;
using LedgerForge.Infrastructure.Procurement;
using LedgerForge.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LedgerForge.IntegrationTests;

public sealed class BulkApprovalAndRenewalPlanningTests
{
    [Fact]
    public async Task BulkSubmissionAndApproval_AreAuditedAndAllOrNothing_WhenSqlGateIsAvailable()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__LedgerForge");
        if (string.IsNullOrWhiteSpace(connection)) return;

        var options = new DbContextOptionsBuilder<LedgerForgeDbContext>()
            .UseSqlServer(connection).Options;
        await using var db = new LedgerForgeDbContext(options);
        var name = Guid.NewGuid().ToString("N")[..8];
        var year = new FiscalYear("Bulk " + name, new DateOnly(2040, 1, 1), new DateOnly(2040, 12, 31), 2040);
        year.SetStatus(FiscalYearStatus.Planning);
        var version = new BudgetVersion(year.Id, "Draft " + name, BudgetVersionType.InitialPlanning, 1);
        var a = new BudgetItem(year.Id, version.Id, "BULK-A-" + name, "1", "Item A", 1m, 10m);
        var b = new BudgetItem(year.Id, version.Id, "BULK-B-" + name, "2", "Item B", 1m, 20m);
        db.FiscalYears.Add(year);
        db.BudgetVersions.Add(version);
        db.BudgetItems.AddRange(a, b);
        await db.SaveChangesAsync();

        var queue = new ApprovalQueueService(db, new BudgetAmendmentService(db), new InvoiceWorkflowService(db));
        var count = await queue.SubmitBudgetItemsAsync(year.Id, version.Id, [a.Id, b.Id], "test-actor");
        Assert.Equal(2, count);
        Assert.Equal(2, await db.BudgetItems.AsNoTracking().CountAsync(x =>
            (x.Id == a.Id || x.Id == b.Id) && x.Status == BudgetItemStatus.Submitted));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            queue.SubmitBudgetItemsAsync(year.Id, version.Id, [a.Id, b.Id], "test-actor"));
        count = await queue.ApproveBudgetItemsAsync([a.Id, b.Id], "test-approver", "Bulk reviewed");
        Assert.Equal(2, count);
        var approved = await db.BudgetItems.AsNoTracking()
            .Where(x => x.Id == a.Id || x.Id == b.Id).ToListAsync();
        Assert.All(approved, item => Assert.Equal(BudgetItemStatus.Approved, item.Status));
        Assert.All(approved, item => Assert.Equal("Bulk reviewed", item.DecisionNote));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            queue.ApproveBudgetItemsAsync([a.Id, b.Id], "test-approver", "Duplicate"));
        Assert.Equal(2, await db.BudgetItems.AsNoTracking().CountAsync(x =>
            (x.Id == a.Id || x.Id == b.Id) && x.Status == BudgetItemStatus.Approved));
    }

    [Fact]
    public async Task ActiveAutoRenewContract_ProjectsNextYearNoticeAndVendor_WhenSqlGateIsAvailable()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__LedgerForge");
        if (string.IsNullOrWhiteSpace(connection)) return;

        var options = new DbContextOptionsBuilder<LedgerForgeDbContext>()
            .UseSqlServer(connection).Options;
        await using var db = new LedgerForgeDbContext(options);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var source = new FiscalYear("Renew src " + suffix, new DateOnly(2043, 1, 1), new DateOnly(2043, 12, 31), 2043);
        var target = new FiscalYear("Renew nxt " + suffix, new DateOnly(2044, 1, 1), new DateOnly(2044, 12, 31), 2044);
        source.SetStatus(FiscalYearStatus.Active);
        target.SetStatus(FiscalYearStatus.Planning);
        var sourceVersion = new BudgetVersion(source.Id, "Source", BudgetVersionType.InitialPlanning, 1);
        var targetVersion = new BudgetVersion(target.Id, "Next FY", BudgetVersionType.InitialPlanning, 1);
        var vendor = new LedgerForge.Domain.Procurement.Vendor("RENEW-" + suffix, "Vendor " + suffix);
        var contract = new LedgerForge.Domain.Procurement.Contract(
            vendor.Id, "CONTRACT-" + suffix, "Auto-renew license",
            new DateOnly(2043, 1, 1), new DateOnly(2043, 11, 15),
            240m, autoRenew: true, renewalNoticeDays: 45);
        contract.Activate("test-actor", DateTimeOffset.UtcNow);

        db.FiscalYears.AddRange(source, target);
        db.BudgetVersions.AddRange(sourceVersion, targetVersion);
        db.Vendors.Add(vendor);
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        var planner = new NextFiscalYearRenewalPlanningService(db, new RenewalProjectionService(db));
        var preview = await planner.GetAsync(source.Id, target.Id, targetVersion.Id, 5m);
        var item = Assert.Single(preview.Candidates, x => x.Reference == contract.ContractNumber);
        Assert.Equal("Auto-renew forecast", item.Source);
        Assert.Equal(new DateOnly(2044, 11, 15), item.RenewalDate);
        Assert.Equal(new DateOnly(2044, 10, 1), item.NoticeDate);
        Assert.Equal(252m, item.ProjectedTotal);
        Assert.Equal(vendor.Id, item.VendorId);

        Assert.Equal(1, await planner.CreateDraftsAsync(
            source.Id, target.Id, targetVersion.Id, 5m, [item.Key]));
        var draft = await db.BudgetItems.AsNoTracking()
            .SingleAsync(x => x.BudgetVersionId == targetVersion.Id);
        Assert.Equal(vendor.Id, draft.VendorId);
        Assert.Equal(BudgetItemStatus.Draft, draft.Status);
    }

    [Fact]
    public async Task NextYearRenewalCarryForward_OnlyCreatesDraftsOnce_WhenSqlGateIsAvailable()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__LedgerForge");
        if (string.IsNullOrWhiteSpace(connection)) return;

        var options = new DbContextOptionsBuilder<LedgerForgeDbContext>()
            .UseSqlServer(connection).Options;
        await using var db = new LedgerForgeDbContext(options);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var source = new FiscalYear("Source " + suffix, new DateOnly(2041, 7, 1), new DateOnly(2042, 6, 30), 2042);
        source.SetStatus(FiscalYearStatus.Active);
        var target = new FiscalYear("Target " + suffix, new DateOnly(2042, 7, 1), new DateOnly(2043, 6, 30), 2043);
        target.SetStatus(FiscalYearStatus.Planning);
        var sourceVersion = new BudgetVersion(source.Id, "Source", BudgetVersionType.InitialPlanning, 1);
        var targetVersion = new BudgetVersion(target.Id, "Next FY", BudgetVersionType.InitialPlanning, 1);

        var original = new BudgetItem(source.Id, sourceVersion.Id, "RENEW-" + suffix,
            "17", "Recurring service", 4m, 25m);
        original.UpdatePlanningDetails("17", "Recurring service", "Needed for operations",
            PurchaseType.Renewal, null, new DateOnly(2041, 12, 15),
            null, null, null, null, null, null, null);
        var oneTime = new BudgetItem(source.Id, sourceVersion.Id, "ONE-" + suffix,
            "18", "One-time purchase", 1m, 99m);

        db.FiscalYears.AddRange(source, target);
        db.BudgetVersions.AddRange(sourceVersion, targetVersion);
        db.BudgetItems.AddRange(original, oneTime);
        await db.SaveChangesAsync();

        var planner = new NextFiscalYearRenewalPlanningService(db, new RenewalProjectionService(db));
        var preview = await planner.GetAsync(source.Id, target.Id, targetVersion.Id, 10m);
        var renewal = Assert.Single(preview.Candidates);
        Assert.Equal("Recurring service", renewal.Description);
        Assert.Equal(new DateOnly(2042, 12, 15), renewal.RenewalDate);
        Assert.Equal(110m, renewal.ProjectedTotal);
        Assert.False(renewal.AlreadyPlanned);

        var created = await planner.CreateDraftsAsync(
            source.Id, target.Id, targetVersion.Id, 10m, [renewal.Key]);
        Assert.Equal(1, created);
        var draft = await db.BudgetItems.AsNoTracking()
            .SingleAsync(x => x.BudgetVersionId == targetVersion.Id);
        Assert.Equal(BudgetItemStatus.Draft, draft.Status);
        Assert.Equal(PurchaseType.Renewal, draft.PurchaseType);
        Assert.Equal(110m, draft.PlannedTotal);
        Assert.Equal(original.StableIdentifier, draft.StableIdentifier);
        Assert.Equal(4m, draft.Quantity);

        preview = await planner.GetAsync(source.Id, target.Id, targetVersion.Id, 10m);
        Assert.True(Assert.Single(preview.Candidates).AlreadyPlanned);
        await Assert.ThrowsAsync<InvalidOperationException>(() => planner.CreateDraftsAsync(
            source.Id, target.Id, targetVersion.Id, 10m, [renewal.Key]));
        Assert.Equal(1, await db.BudgetItems.AsNoTracking()
            .CountAsync(x => x.BudgetVersionId == targetVersion.Id));
    }
}
