using Crch.ItBudget.Domain.Budgeting;
using Crch.ItBudget.Domain.Importing;
using Crch.ItBudget.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Crch.ItBudget.Infrastructure.Persistence;

public sealed class ItBudgetDbContext(DbContextOptions<ItBudgetDbContext> options) : DbContext(options)
{
    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();
    public DbSet<FiscalPeriod> FiscalPeriods => Set<FiscalPeriod>();
    public DbSet<BudgetVersion> BudgetVersions => Set<BudgetVersion>();
    public DbSet<BudgetItem> BudgetItems => Set<BudgetItem>();
    public DbSet<BudgetItemAllocation> BudgetItemAllocations => Set<BudgetItemAllocation>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();
    public DbSet<ImportException> ImportExceptions => Set<ImportException>();

    public DbSet<BudgetSection> BudgetSections => Set<BudgetSection>();
    public DbSet<FinanceType> FinanceTypes => Set<FinanceType>();
    public DbSet<FinanceAccount> FinanceAccounts => Set<FinanceAccount>();
    public DbSet<FinanceCategory> FinanceCategories => Set<FinanceCategory>();
    public DbSet<InternalCategory> InternalCategories => Set<InternalCategory>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<NeedLevel> NeedLevels => Set<NeedLevel>();
    public DbSet<PriorityLookup> Priorities => Set<PriorityLookup>();
    public DbSet<Frequency> Frequencies => Set<Frequency>();
    public DbSet<PurchaseTypeLookup> PurchaseTypes => Set<PurchaseTypeLookup>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<ApprovalStatusLookup> ApprovalStatuses => Set<ApprovalStatusLookup>();
    public DbSet<TransactionTypeLookup> TransactionTypes => Set<TransactionTypeLookup>();
    public DbSet<PurchaseOrderStatusLookup> PurchaseOrderStatuses => Set<PurchaseOrderStatusLookup>();
    public DbSet<InvoiceStatusLookup> InvoiceStatuses => Set<InvoiceStatusLookup>();
    public DbSet<RenewalStatusLookup> RenewalStatuses => Set<RenewalStatusLookup>();
    public DbSet<ContractStatusLookup> ContractStatuses => Set<ContractStatusLookup>();
    public DbSet<LookupValueAlias> LookupValueAliases => Set<LookupValueAlias>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FiscalYear>(entity =>
        {
            entity.ToTable("FiscalYear", table =>
            {
                table.HasCheckConstraint("CK_FiscalYear_DateRange", "[EndDate] >= [StartDate]");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DisplayName).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.DisplayName).IsUnique();
            entity.HasIndex(x => x.IsCurrent).HasFilter("[IsCurrent] = 1").IsUnique();
            entity.HasIndex(x => new { x.StartDate, x.EndDate });
        });

        modelBuilder.Entity<FiscalPeriod>(entity =>
        {
            entity.ToTable("FiscalPeriod", table =>
            {
                table.HasCheckConstraint("CK_FiscalPeriod_Number", "[PeriodNumber] >= 1 AND [PeriodNumber] <= 53");
                table.HasCheckConstraint("CK_FiscalPeriod_DateRange", "[EndDate] >= [StartDate]");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ClosedBy).HasMaxLength(256);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.FiscalYearId, x.PeriodNumber }).IsUnique();
            entity.HasIndex(x => new { x.FiscalYearId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.FiscalYearId, x.StartDate, x.EndDate });
            entity.HasOne<FiscalYear>()
                .WithMany()
                .HasForeignKey(x => x.FiscalYearId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BudgetVersion>(entity =>
        {
            entity.ToTable("BudgetVersion");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.FiscalYearId, x.VersionNumber }).IsUnique();
            entity.HasOne<FiscalYear>()
                .WithMany()
                .HasForeignKey(x => x.FiscalYearId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BudgetItem>(entity =>
        {
            entity.ToTable("BudgetItem", table =>
            {
                table.HasCheckConstraint("CK_BudgetItem_Quantity", "[Quantity] >= 0");
                table.HasCheckConstraint("CK_BudgetItem_UnitCost", "[UnitCost] >= 0");
                table.HasCheckConstraint("CK_BudgetItem_PlannedTotal", "[PlannedTotal] >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StableIdentifier).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ItemNumber).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ReasonPurpose).HasMaxLength(2000);
            entity.Property(x => x.Quantity).HasPrecision(19, 4);
            entity.Property(x => x.UnitCost).HasPrecision(19, 4);
            entity.Property(x => x.PlannedTotal).HasPrecision(19, 4);
            entity.Property(x => x.ApprovedTotal).HasPrecision(19, 4);
            entity.Property(x => x.RevisedTotal).HasPrecision(19, 4);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.FiscalYearId, x.BudgetVersionId, x.ItemNumber }).IsUnique();
            entity.HasIndex(x => x.StableIdentifier);
            entity.HasIndex(x => x.Status);
            entity.HasOne<FiscalYear>()
                .WithMany()
                .HasForeignKey(x => x.FiscalYearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<BudgetVersion>()
                .WithMany()
                .HasForeignKey(x => x.BudgetVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BudgetItemAllocation>(entity =>
        {
            entity.ToTable("BudgetItemAllocation", table =>
            {
                table.HasCheckConstraint(
                    "CK_BudgetItemAllocation_Value",
                    "([Method] = 0 AND [Percentage] IS NOT NULL AND [Percentage] >= 0 AND [Percentage] <= 100 AND [Amount] IS NULL) OR " +
                    "([Method] = 1 AND [Amount] IS NOT NULL AND [Amount] >= 0 AND [Percentage] IS NULL)");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Percentage).HasPrecision(9, 4);
            entity.Property(x => x.Amount).HasPrecision(19, 4);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.BudgetItemId);
            entity.HasIndex(x => x.DepartmentId);
            entity.HasIndex(x => x.LocationId);
            entity.HasIndex(x => x.FinanceAccountId);
            entity.HasIndex(x => x.FiscalPeriodId);
            entity.HasOne<BudgetItem>()
                .WithMany()
                .HasForeignKey(x => x.BudgetItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Department>()
                .WithMany()
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Location>()
                .WithMany()
                .HasForeignKey(x => x.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinanceAccount>()
                .WithMany()
                .HasForeignKey(x => x.FinanceAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FiscalPeriod>()
                .WithMany()
                .HasForeignKey(x => x.FiscalPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        ConfigureManagedLookups(modelBuilder);
        ConfigureImports(modelBuilder);
    }

    private static void ConfigureManagedLookups(ModelBuilder modelBuilder)
    {
        var lookupBase = modelBuilder.Entity<ManagedLookupEntity>();
        lookupBase.UseTpcMappingStrategy();
        lookupBase.HasKey(x => x.Id);
        lookupBase.Property(x => x.Code).HasMaxLength(100).IsRequired();
        lookupBase.Property(x => x.Name).HasMaxLength(250).IsRequired();
        lookupBase.Property(x => x.Description).HasMaxLength(1000);
        lookupBase.Property(x => x.RowVersion).IsRowVersion();

        ConfigureLookup<BudgetSection>(modelBuilder, "BudgetSection");
        ConfigureLookup<FinanceType>(modelBuilder, "FinanceType");
        ConfigureLookup<FinanceAccount>(modelBuilder, "FinanceAccount");
        ConfigureLookup<FinanceCategory>(modelBuilder, "FinanceCategory");
        ConfigureLookup<InternalCategory>(modelBuilder, "InternalCategory");
        ConfigureLookup<Department>(modelBuilder, "Department");
        ConfigureLookup<Location>(modelBuilder, "Location");
        ConfigureLookup<NeedLevel>(modelBuilder, "NeedLevel");
        ConfigureLookup<PriorityLookup>(modelBuilder, "Priority");
        ConfigureLookup<Frequency>(modelBuilder, "Frequency");
        ConfigureLookup<PurchaseTypeLookup>(modelBuilder, "PurchaseType");
        ConfigureLookup<UnitOfMeasure>(modelBuilder, "UnitOfMeasure");
        ConfigureLookup<DocumentType>(modelBuilder, "DocumentType");
        ConfigureLookup<ApprovalStatusLookup>(modelBuilder, "ApprovalStatus");
        ConfigureLookup<TransactionTypeLookup>(modelBuilder, "TransactionType");
        ConfigureLookup<PurchaseOrderStatusLookup>(modelBuilder, "PurchaseOrderStatus");
        ConfigureLookup<InvoiceStatusLookup>(modelBuilder, "InvoiceStatus");
        ConfigureLookup<RenewalStatusLookup>(modelBuilder, "RenewalStatus");
        ConfigureLookup<ContractStatusLookup>(modelBuilder, "ContractStatus");

        modelBuilder.Entity<FinanceAccount>()
            .HasOne<FinanceCategory>()
            .WithMany()
            .HasForeignKey(x => x.FinanceCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LookupValueAlias>(entity =>
        {
            entity.ToTable("LookupValueAlias");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LookupType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SourceValue).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CanonicalCode).HasMaxLength(100).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.LookupType, x.SourceValue }).IsUnique();
            entity.HasIndex(x => new { x.LookupType, x.CanonicalCode, x.IsActive });
        });
    }

    private static void ConfigureLookup<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : ManagedLookupEntity
    {
        var entity = modelBuilder.Entity<TEntity>();
        entity.ToTable(tableName, table =>
        {
            table.HasCheckConstraint($"CK_{tableName}_SortOrder", "[SortOrder] >= 0");
        });
        entity.HasIndex(x => x.Code).IsUnique();
        entity.HasIndex(x => new { x.IsActive, x.SortOrder, x.Name });
    }

    private static void ConfigureImports(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.ToTable("ImportBatch", table =>
            {
                table.HasCheckConstraint("CK_ImportBatch_SourceSize", "[SourceSizeBytes] >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ImportType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SourceFileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.SourceSha256).HasMaxLength(64).IsUnicode(false).IsRequired();
            entity.Property(x => x.RecalculatedPlannedTotal).HasPrecision(19, 4);
            entity.Property(x => x.AcceptedBy).HasMaxLength(256);
            entity.Property(x => x.AcceptanceReason).HasMaxLength(2000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.SourceSha256);
            entity.HasIndex(x => new { x.ImportType, x.Status });
            entity.HasIndex(x => x.CreatedAtUtc);
        });

        modelBuilder.Entity<ImportRow>(entity =>
        {
            entity.ToTable("ImportRow", table =>
            {
                table.HasCheckConstraint("CK_ImportRow_SourceRowNumber", "[SourceRowNumber] >= 1");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceSheet).HasMaxLength(128).IsRequired();
            entity.Property(x => x.SourceKey).HasMaxLength(256);
            entity.Property(x => x.RawDataJson).IsRequired();
            entity.Property(x => x.TargetEntityType).HasMaxLength(128);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.ImportBatchId, x.SourceSheet, x.SourceRowNumber }).IsUnique();
            entity.HasIndex(x => new { x.ImportBatchId, x.Outcome });
            entity.HasOne<ImportBatch>()
                .WithMany()
                .HasForeignKey(x => x.ImportBatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ImportException>(entity =>
        {
            entity.ToTable("ImportException");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.AssignedTo).HasMaxLength(256);
            entity.Property(x => x.ResolutionNote).HasMaxLength(2000);
            entity.Property(x => x.ResolvedBy).HasMaxLength(256);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.ImportBatchId, x.ResolutionStatus });
            entity.HasIndex(x => new { x.Code, x.Severity });
            entity.HasOne<ImportBatch>()
                .WithMany()
                .HasForeignKey(x => x.ImportBatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ImportRow>()
                .WithMany()
                .HasForeignKey(x => x.ImportRowId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
