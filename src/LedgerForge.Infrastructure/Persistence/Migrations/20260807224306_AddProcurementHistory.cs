using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChangeOrderSequence",
                table: "PurchaseOrder",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesPurchaseOrderId",
                table: "PurchaseOrder",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PurchaseReceipt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReceivedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReceivedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReceipt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseReceipt_PurchaseOrder_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReceiptLine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityReceived = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReceiptLine", x => x.Id);
                    table.CheckConstraint("CK_PurchaseReceiptLine_Quantity", "[QuantityReceived] > 0");
                    table.ForeignKey(
                        name: "FK_PurchaseReceiptLine_PurchaseOrderLine_PurchaseOrderLineId",
                        column: x => x.PurchaseOrderLineId,
                        principalTable: "PurchaseOrderLine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReceiptLine_PurchaseReceipt_PurchaseReceiptId",
                        column: x => x.PurchaseReceiptId,
                        principalTable: "PurchaseReceipt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrder_SupersedesPurchaseOrderId",
                table: "PurchaseOrder",
                column: "SupersedesPurchaseOrderId",
                unique: true,
                filter: "[SupersedesPurchaseOrderId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseOrder_ChangeOrder",
                table: "PurchaseOrder",
                sql: "([SupersedesPurchaseOrderId] IS NULL AND [ChangeOrderSequence] = 0) OR ([SupersedesPurchaseOrderId] IS NOT NULL AND [ChangeOrderSequence] > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipt_PurchaseOrderId_ReceiptNumber",
                table: "PurchaseReceipt",
                columns: new[] { "PurchaseOrderId", "ReceiptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipt_PurchaseOrderId_ReceivedDate",
                table: "PurchaseReceipt",
                columns: new[] { "PurchaseOrderId", "ReceivedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceiptLine_PurchaseOrderLineId",
                table: "PurchaseReceiptLine",
                column: "PurchaseOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceiptLine_PurchaseReceiptId_PurchaseOrderLineId",
                table: "PurchaseReceiptLine",
                columns: new[] { "PurchaseReceiptId", "PurchaseOrderLineId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrder_PurchaseOrder_SupersedesPurchaseOrderId",
                table: "PurchaseOrder",
                column: "SupersedesPurchaseOrderId",
                principalTable: "PurchaseOrder",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrder_PurchaseOrder_SupersedesPurchaseOrderId",
                table: "PurchaseOrder");

            migrationBuilder.DropTable(
                name: "PurchaseReceiptLine");

            migrationBuilder.DropTable(
                name: "PurchaseReceipt");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrder_SupersedesPurchaseOrderId",
                table: "PurchaseOrder");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseOrder_ChangeOrder",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "ChangeOrderSequence",
                table: "PurchaseOrder");

            migrationBuilder.DropColumn(
                name: "SupersedesPurchaseOrderId",
                table: "PurchaseOrder");
        }
    }
}
