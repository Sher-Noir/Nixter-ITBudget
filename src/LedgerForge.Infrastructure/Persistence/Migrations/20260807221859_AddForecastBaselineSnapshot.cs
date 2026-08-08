using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddForecastBaselineSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BaselineTotal",
                table: "ForecastLine",
                type: "decimal(19,4)",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [ForecastLine]
                SET [BaselineTotal] = [ForecastTotal]
                WHERE [BaselineTotal] IS NULL;
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "BaselineTotal",
                table: "ForecastLine",
                type: "decimal(19,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaselineTotal",
                table: "ForecastLine");
        }
    }
}
