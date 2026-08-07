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
            typeof(PurchaseReceipt),
            typeof(PurchaseReceiptLine),
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
            (typeof(ForecastLine), nameof(ForecastLine.BaselineTotal)),
            (typeof(ForecastLine), nameof(ForecastLine.ForecastTotal)),
            (typeof(ActualTransaction), nameof(ActualTransaction.Amount)),
            (typeof(PurchaseOrderLine), nameof(PurchaseOrderLine.Quantity)),
            (typeof(PurchaseOrderLine), nameof(PurchaseOrderLine.UnitCost)),
            (typeof(PurchaseOrderLine), nameof(PurchaseOrderLine.LineTotal)),
            (typeof(PurchaseReceiptLine), nameof(PurchaseReceiptLine.QuantityReceived)),
            (typeof(Invoice), nameof(Invoice.TotalAmount)),
            (typeof(InvoiceAllocation), nameof(InvoiceAllocation.Amount)),
            (typeof(Contract), nameof(Contract.EstimatedAnnualAmount)),
            (typeof(ContractRenewal), nameof(ContractRenewal.ExpectedAmount))
        };

        foreach (var (entityType, propertyName) in financialProperties)
        {
            var property = context.Model.FindEntityType(entityType)?.FindProperty(propertyName);
            Assert.NotNull(property);

            var precision = property!.GetPrecision();
            var scale = property.GetScale();
            var columnType = property.GetColumnType();
            var isDecimal19Scale4 =
                (precision == 19 && scale == 4) ||
                string.Equals(columnType?.Replace(" ", string.Empty, StringComparison.Ordinal), "decimal(19,4)", StringComparison.OrdinalIgnoreCase);

            Assert.True(
                isDecimal19Scale4,
                $"{entityType.Name}.{propertyName} must map to decimal(19,4); precision={precision?.ToString() ?? "null"}, scale={scale?.ToString() ?? "null"}, columnType={columnType ?? "null"}.");
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

    [Fact]
    public void PurchaseOrderChangeOrderLineage_IsRestrictiveAndUnique()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(PurchaseOrder));
        Assert.NotNull(entity);
        var foreignKey = entity!.GetForeignKeys().Single(x => x.Properties.Single().Name == nameof(PurchaseOrder.SupersedesPurchaseOrderId));
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        var lineageIndex = entity.GetIndexes().Single(x => x.Properties.Count == 1 && x.Properties[0].Name == nameof(PurchaseOrder.SupersedesPurchaseOrderId));
        Assert.True(lineageIndex.IsUnique);
    }
}
