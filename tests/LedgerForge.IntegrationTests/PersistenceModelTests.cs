using LedgerForge.Domain.Actuals;
using LedgerForge.Domain.Auditing;
using LedgerForge.Domain.Budgeting;
using LedgerForge.Domain.Procurement;
using LedgerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LedgerForge.IntegrationTests;

public sealed class PersistenceModelTests
{
    private static LedgerForgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LedgerForgeDbContext>()
            .UseSqlServer("Server=(local);Database=LedgerForge_ModelValidation;Integrated Security=True;TrustServerCertificate=True")
            .Options;
        return new LedgerForgeDbContext(options);
    }

    [Fact]
    public void Model_IncludesShippedFinancialAndAuditEntities()
    {
        using var context = CreateContext();
        var expected = new[]
        {
            typeof(FiscalYear),
            typeof(FiscalPeriod),
            typeof(BudgetVersion),
            typeof(BudgetItem),
            typeof(BudgetItemAllocation),
            typeof(BudgetAmendment),
            typeof(ForecastScenario),
            typeof(ForecastLine),
            typeof(ActualTransaction),
            typeof(Vendor),
            typeof(PurchaseOrder),
            typeof(PurchaseOrderLine),
            typeof(Invoice),
            typeof(InvoiceAllocation),
            typeof(Contract),
            typeof(ContractRenewal),
            typeof(AuditEvent)
        };

        Assert.All(expected, type => Assert.NotNull(context.Model.FindEntityType(type)));
    }

    [Fact]
    public void Model_UsesDecimal19Scale4ForFinancialAmounts()
    {
        using var context = CreateContext();
        var financialProperties = new (Type EntityType, string PropertyName)[]
        {
            (typeof(BudgetItem), nameof(BudgetItem.Quantity)),
            (typeof(BudgetItem), nameof(BudgetItem.UnitCost)),
            (typeof(BudgetItem), nameof(BudgetItem.PlannedTotal)),
            (typeof(BudgetItem), nameof(BudgetItem.ApprovedTotal)),
            (typeof(BudgetItem), nameof(BudgetItem.RevisedTotal)),
            (typeof(BudgetItemAllocation), nameof(BudgetItemAllocation.Amount)),
            (typeof(BudgetAmendment), nameof(BudgetAmendment.AmountDelta)),
            (typeof(BudgetAmendment), nameof(BudgetAmendment.ResultingRevisedTotal)),
            (typeof(ForecastLine), nameof(ForecastLine.ForecastTotal)),
            (typeof(ActualTransaction), nameof(ActualTransaction.Amount)),
            (typeof(PurchaseOrderLine), nameof(PurchaseOrderLine.Quantity)),
            (typeof(PurchaseOrderLine), nameof(PurchaseOrderLine.UnitCost)),
            (typeof(PurchaseOrderLine), nameof(PurchaseOrderLine.LineTotal)),
            (typeof(Invoice), nameof(Invoice.TotalAmount)),
            (typeof(InvoiceAllocation), nameof(InvoiceAllocation.Amount)),
            (typeof(Contract), nameof(Contract.EstimatedAnnualAmount)),
            (typeof(ContractRenewal), nameof(ContractRenewal.ExpectedAmount))
        };

        foreach (var (entityType, propertyName) in financialProperties)
        {
            var property = context.Model.FindEntityType(entityType)?.FindProperty(propertyName);
            Assert.NotNull(property);
            Assert.Equal(19, property!.GetPrecision());
            Assert.Equal(4, property.GetScale());
        }
    }

    [Fact]
    public void Model_DoesNotCascadeDeleteFinancialHistory()
    {
        using var context = CreateContext();
        var unexpected = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetForeignKeys())
            .Where(foreignKey => foreignKey.DeleteBehavior is DeleteBehavior.Cascade or DeleteBehavior.ClientCascade)
            .Select(foreignKey => $"{foreignKey.DeclaringEntityType.ClrType.Name}->{foreignKey.PrincipalEntityType.ClrType.Name}")
            .OrderBy(value => value)
            .ToArray();

        Assert.Empty(unexpected);
    }
}