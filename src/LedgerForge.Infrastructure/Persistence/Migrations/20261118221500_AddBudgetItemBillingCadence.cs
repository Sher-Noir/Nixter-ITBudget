using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerForge.Infrastructure.Persistence.Migrations;

public partial class AddBudgetItemBillingCadence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "BillingCadence",
            table: "BudgetItem",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddCheckConstraint(
            name: "CK_BudgetItem_BillingCadence",
            table: "BudgetItem",
            sql: "[BillingCadence] BETWEEN 0 AND 3");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_BudgetItem_BillingCadence",
            table: "BudgetItem");
        migrationBuilder.DropColumn(name: "BillingCadence", table: "BudgetItem");
    }
}
