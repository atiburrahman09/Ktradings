using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTrading.Migrations
{
    /// <inheritdoc />
    public partial class AddOutsideSalesDamageReturnFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOutsideSalesDamageReturn",
                table: "ProductReturnItems",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOutsideSalesDamageReturn",
                table: "ProductReturnItems");
        }
    }
}
