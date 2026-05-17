using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTrading.Migrations
{
    public partial class AddProductReturnDamagedQuantity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DamagedQuantity",
                table: "ProductReturnItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE [ProductReturnItems]
                SET [DamagedQuantity] = [Quantity]
                WHERE [IsDamaged] = 1 AND [DamagedQuantity] = 0
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DamagedQuantity",
                table: "ProductReturnItems");
        }
    }
}
