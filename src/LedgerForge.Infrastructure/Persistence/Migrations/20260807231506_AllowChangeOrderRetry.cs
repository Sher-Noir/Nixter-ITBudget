using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowChangeOrderRetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrder_SupersedesPurchaseOrderId",
                table: "PurchaseOrder");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_SupersedesPurchaseOrderId",
                table: "PurchaseOrder",
                column: "SupersedesPurchaseOrderId",
                unique: true,
                filter: "[SupersedesPurchaseOrderId] IS NOT NULL AND [State] <> 5 AND [State] <> 6");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrder_SupersedesPurchaseOrderId",
                table: "PurchaseOrder");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_SupersedesPurchaseOrderId",
                table: "PurchaseOrder",
                column: "SupersedesPurchaseOrderId",
                unique: true,
                filter: "[SupersedesPurchaseOrderId] IS NOT NULL");
        }
    }
}
