using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTrading.Migrations
{
    /// <inheritdoc />
    public partial class LinkProductReturnsToSalesOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SalesOrderId",
                table: "ProductReturns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductReturns_SalesOrderId",
                table: "ProductReturns",
                column: "SalesOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductReturns_SalesOrderId",
                table: "ProductReturns");

            migrationBuilder.DropColumn(
                name: "SalesOrderId",
                table: "ProductReturns");
        }
    }
}
