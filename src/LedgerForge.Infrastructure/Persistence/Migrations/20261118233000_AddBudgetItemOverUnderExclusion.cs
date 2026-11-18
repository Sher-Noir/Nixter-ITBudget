using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerForge.Infrastructure.Persistence.Migrations;

public partial class AddBudgetItemOverUnderExclusion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "ExcludeFromOverUnder",
            table: "BudgetItem",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ExcludeFromOverUnder", table: "BudgetItem");
    }
}
