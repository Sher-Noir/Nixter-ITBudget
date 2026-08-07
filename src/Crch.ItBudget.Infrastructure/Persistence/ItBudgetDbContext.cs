using Crch.ItBudget.Domain.Budgeting;
using Microsoft.EntityFrameworkCore;

namespace Crch.ItBudget.Infrastructure.Persistence;

public sealed class ItBudgetDbContext(DbContextOptions<ItBudgetDbContext> options) : DbContext(options)
{
    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();
    public DbSet<BudgetVersion> BudgetVersions => Set<BudgetVersion>();
    public DbSet<BudgetItem> BudgetItems => Set<BudgetItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FiscalYear>(entity =>
        {
            entity.ToTable("FiscalYear");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DisplayName).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.IsCurrent).HasFilter("[IsCurrent] = 1").IsUnique();
        });

        modelBuilder.Entity<BudgetVersion>(entity =>
        {
            entity.ToTable("BudgetVersion");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.FiscalYearId, x.VersionNumber }).IsUnique();
        });

        modelBuilder.Entity<BudgetItem>(entity =>
        {
            entity.ToTable("BudgetItem");
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
        });
    }
}
