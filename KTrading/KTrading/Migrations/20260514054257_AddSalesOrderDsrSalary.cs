using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTrading.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOrderDsrSalary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DsrSalary",
                table: "SalesOrders",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DsrSalary",
                table: "SalesOrders");
        }
    }
}
