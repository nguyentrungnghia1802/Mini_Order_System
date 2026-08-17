using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroShop.NotificationService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialNotificationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consumed_messages",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    consumer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    consumed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    trace_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consumed_messages", x => x.message_id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.CheckConstraint("ck_notifications_body_not_blank", "length(btrim(body)) > 0");
                    table.CheckConstraint("ck_notifications_currency_length", "length(currency) = 3");
                    table.CheckConstraint("ck_notifications_customer_email_not_blank", "length(btrim(customer_email)) > 0");
                    table.CheckConstraint("ck_notifications_subject_not_blank", "length(btrim(subject)) > 0");
                    table.CheckConstraint("ck_notifications_total_nonnegative", "total_amount >= 0");
                    table.ForeignKey(
                        name: "FK_notifications_consumed_messages_source_message_id",
                        column: x => x.source_message_id,
                        principalTable: "consumed_messages",
                        principalColumn: "message_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consumed_messages_consumer_consumed_at",
                table: "consumed_messages",
                columns: new[] { "consumer", "consumed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_customer_email_created_at",
                table: "notifications",
                columns: new[] { "customer_email", "created_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_order_id",
                table: "notifications",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ux_notifications_source_message_id",
                table: "notifications",
                column: "source_message_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "consumed_messages");
        }
    }
}
