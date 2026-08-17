using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroShop.ProductService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    released_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_reservations", x => x.id);
                    table.CheckConstraint("ck_inventory_reservations_release_timestamp_consistent", "(status = 'released' AND released_at_utc IS NOT NULL) OR (status = 'reserved' AND released_at_utc IS NULL)");
                    table.CheckConstraint("ck_inventory_reservations_status_valid", "status IN ('reserved', 'released')");
                });

            migrationBuilder.CreateTable(
                name: "inventory_reservation_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_reservation_items", x => x.id);
                    table.CheckConstraint("ck_inventory_reservation_items_quantity_positive", "quantity > 0");
                    table.CheckConstraint("ck_inventory_reservation_items_status_valid", "status IN ('reserved', 'released')");
                    table.CheckConstraint("ck_inventory_reservation_items_subtotal_nonnegative", "subtotal >= 0");
                    table.ForeignKey(
                        name: "FK_inventory_reservation_items_inventory_reservations_reservat~",
                        column: x => x.reservation_id,
                        principalTable: "inventory_reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_reservation_items_product_id",
                table: "inventory_reservation_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ux_inventory_reservation_items_reservation_product",
                table: "inventory_reservation_items",
                columns: new[] { "reservation_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_reservations_status_created_at",
                table: "inventory_reservations",
                columns: new[] { "status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_inventory_reservations_order_id",
                table: "inventory_reservations",
                column: "order_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_reservation_items");

            migrationBuilder.DropTable(
                name: "inventory_reservations");
        }
    }
}
