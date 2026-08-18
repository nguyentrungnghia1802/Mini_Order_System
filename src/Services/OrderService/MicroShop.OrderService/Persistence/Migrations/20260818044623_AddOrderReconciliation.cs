using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroShop.OrderService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_inventory_request_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_inventory_request_items", x => x.id);
                    table.CheckConstraint("ck_order_inventory_request_items_quantity_positive", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_order_inventory_request_items_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_reconciliation_audits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    from_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    to_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reservation_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    trace_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_reconciliation_audits", x => x.id);
                    table.CheckConstraint("ck_order_reconciliation_audits_operation_valid", "operation IN ('inventory', 'cancellation')");
                    table.ForeignKey(
                        name: "FK_order_reconciliation_audits_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_order_inventory_request_items_product_id",
                table: "order_inventory_request_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ux_order_inventory_request_items_order_product",
                table: "order_inventory_request_items",
                columns: new[] { "order_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_order_reconciliation_audits_order_time",
                table: "order_reconciliation_audits",
                columns: new[] { "order_id", "occurred_at_utc", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_inventory_request_items");

            migrationBuilder.DropTable(
                name: "order_reconciliation_audits");
        }
    }
}
