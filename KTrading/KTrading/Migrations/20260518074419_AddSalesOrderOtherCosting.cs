using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTrading.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOrderOtherCosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OtherCosting",
                table: "SalesOrders",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "OtherCostingNote",
                table: "SalesOrders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OtherCosting",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "OtherCostingNote",
                table: "SalesOrders");
        }
    }
}
