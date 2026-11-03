using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerForge.Infrastructure.Persistence.Migrations;

public partial class AddBudgetItemVendorAndNotes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Notes",
            table: "BudgetItem",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "VendorId",
            table: "BudgetItem",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_BudgetItem_VendorId",
            table: "BudgetItem",
            column: "VendorId");

        migrationBuilder.AddForeignKey(
            name: "FK_BudgetItem_Vendor_VendorId",
            table: "BudgetItem",
            column: "VendorId",
            principalTable: "Vendor",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_BudgetItem_Vendor_VendorId",
            table: "BudgetItem");

        migrationBuilder.DropIndex(
            name: "IX_BudgetItem_VendorId",
            table: "BudgetItem");

        migrationBuilder.DropColumn(
            name: "Notes",
            table: "BudgetItem");

        migrationBuilder.DropColumn(
            name: "VendorId",
            table: "BudgetItem");
    }
}
