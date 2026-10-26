using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WholesaleRetailStore.Migrations
{
    /// <inheritdoc />
    public partial class OrderDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountDescription",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "PricingRules",
                keyColumn: "Id",
                keyValue: 2,
                column: "Description",
                value: "Volume discount - 15% off for orders over R500");

            // Orders placed before this migration stored the order-level discount only inside
            // TotalAmount. Recover it so that sum(items) - DiscountAmount = TotalAmount holds.
            migrationBuilder.Sql(@"
                UPDATE o
                SET o.DiscountAmount = i.ItemsTotal - o.TotalAmount
                FROM Orders o
                JOIN (SELECT OrderId, SUM(Quantity * UnitPrice) AS ItemsTotal FROM OrderItems GROUP BY OrderId) i
                    ON i.OrderId = o.Id
                WHERE i.ItemsTotal > o.TotalAmount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountDescription",
                table: "Orders");

            migrationBuilder.UpdateData(
                table: "PricingRules",
                keyColumn: "Id",
                keyValue: 2,
                column: "Description",
                value: "Volume discount - 15% off for orders over $500");
        }
    }
}
