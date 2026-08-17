using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroShop.ProductService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryReservationConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_inventory_reservations_currency_vnd",
                table: "inventory_reservations",
                sql: "currency = 'VND'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_inventory_reservations_total_nonnegative",
                table: "inventory_reservations",
                sql: "total_amount >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_inventory_reservation_items_unit_price_nonnegative",
                table: "inventory_reservation_items",
                sql: "unit_price >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_reservation_items_products_product_id",
                table: "inventory_reservation_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_inventory_reservation_items_products_product_id",
                table: "inventory_reservation_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_inventory_reservations_currency_vnd",
                table: "inventory_reservations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_inventory_reservations_total_nonnegative",
                table: "inventory_reservations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_inventory_reservation_items_unit_price_nonnegative",
                table: "inventory_reservation_items");
        }
    }
}
