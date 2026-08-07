using Crch.ItBudget.Domain.Budgeting;
using Crch.ItBudget.Domain.Importing;
using Microsoft.EntityFrameworkCore;

namespace Crch.ItBudget.Infrastructure.Persistence;

public sealed class ItBudgetDbContext(DbContextOptions<ItBudgetDbContext> options) : DbContext(options)
{
    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();
    public DbSet<BudgetVersion> BudgetVersions => Set<BudgetVersion>();
    public DbSet<BudgetItem> BudgetItems => Set<BudgetItem>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();
    public DbSet<ImportException> ImportExceptions => Set<ImportException>();

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
