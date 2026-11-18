using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerForge.Infrastructure.Persistence.Migrations;

public partial class AddRecurringActualSchedules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RecurringActualSchedule",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BudgetItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                LastDayOfMonth = table.Column<bool>(type: "bit", nullable: false),
                DayOfMonth = table.Column<int>(type: "int", nullable: false),
                StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                SourceReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                FinanceAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IsArchived = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RecurringActualSchedule", x => x.Id);
                table.CheckConstraint("CK_RecurringActualSchedule_Amount", "[Amount] > 0");
                table.CheckConstraint("CK_RecurringActualSchedule_Dates", "[EndDate] >= [StartDate]");
                table.CheckConstraint("CK_RecurringActualSchedule_Day", "[DayOfMonth] BETWEEN 1 AND 31");
                table.ForeignKey(
                    name: "FK_RecurringActualSchedule_BudgetItem_BudgetItemId",
                    column: x => x.BudgetItemId, principalTable: "BudgetItem",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RecurringActualSchedule_FiscalYear_FiscalYearId",
                    column: x => x.FiscalYearId, principalTable: "FiscalYear",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RecurringActualSchedule_FinanceAccount_FinanceAccountId",
                    column: x => x.FinanceAccountId, principalTable: "FinanceAccount",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RecurringActualSchedule_Department_DepartmentId",
                    column: x => x.DepartmentId, principalTable: "Department",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RecurringActualSchedule_Location_LocationId",
                    column: x => x.LocationId, principalTable: "Location",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "RecurringActualOccurrence",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActualTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BillingYear = table.Column<int>(type: "int", nullable: false),
                BillingMonth = table.Column<int>(type: "int", nullable: false),
                BillingDate = table.Column<DateOnly>(type: "date", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IsArchived = table.Column<bool>(type: "bit", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RecurringActualOccurrence", x => x.Id);
                table.CheckConstraint("CK_RecurringActualOccurrence_Month", "[BillingMonth] BETWEEN 1 AND 12");
                table.CheckConstraint("CK_RecurringActualOccurrence_Year", "[BillingYear] BETWEEN 2000 AND 9999");
                table.ForeignKey(
                    name: "FK_RecurringActualOccurrence_RecurringActualSchedule_ScheduleId",
                    column: x => x.ScheduleId, principalTable: "RecurringActualSchedule",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_RecurringActualOccurrence_ActualTransaction_ActualTransactionId",
                    column: x => x.ActualTransactionId, principalTable: "ActualTransaction",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_RecurringActualSchedule_BudgetItemId",
            "RecurringActualSchedule", "BudgetItemId");
        migrationBuilder.CreateIndex("IX_RecurringActualSchedule_FiscalYearId",
            "RecurringActualSchedule", "FiscalYearId");
        migrationBuilder.CreateIndex("IX_RecurringActualSchedule_FinanceAccountId",
            "RecurringActualSchedule", "FinanceAccountId");
        migrationBuilder.CreateIndex("IX_RecurringActualSchedule_DepartmentId",
            "RecurringActualSchedule", "DepartmentId");
        migrationBuilder.CreateIndex("IX_RecurringActualSchedule_LocationId",
            "RecurringActualSchedule", "LocationId");
        migrationBuilder.CreateIndex("IX_RecurringActualSchedule_IsActive_StartDate",
            "RecurringActualSchedule", new[] { "IsActive", "StartDate" });

        migrationBuilder.CreateIndex("IX_RecurringActualOccurrence_ActualTransactionId",
            "RecurringActualOccurrence", "ActualTransactionId", unique: true);
        migrationBuilder.CreateIndex("IX_RecurringActualOccurrence_ScheduleId_BillingYear_BillingMonth",
            "RecurringActualOccurrence", new[] { "ScheduleId", "BillingYear", "BillingMonth" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RecurringActualOccurrence");
        migrationBuilder.DropTable(name: "RecurringActualSchedule");
    }
}
