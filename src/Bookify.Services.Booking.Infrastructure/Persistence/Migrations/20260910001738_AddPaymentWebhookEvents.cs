using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Services.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentWebhookEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_webhook_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    event_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    event_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    received_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    processing_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    error_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_webhook_events", x => x.id);
                    table.CheckConstraint("ck_payment_webhook_events_processed_time", "processed_at_utc IS NULL\nOR\nprocessed_at_utc >= received_at_utc");
                    table.CheckConstraint("ck_payment_webhook_events_state", "(\n    processing_status = 'Processing'\n    AND processed_at_utc IS NULL\n    AND error_code IS NULL\n    AND error_message IS NULL\n)\nOR\n(\n    processing_status = 'Processed'\n    AND processed_at_utc IS NOT NULL\n    AND error_code IS NULL\n    AND error_message IS NULL\n)\nOR\n(\n    processing_status = 'Failed'\n    AND processed_at_utc IS NULL\n    AND error_code IS NOT NULL\n    AND error_message IS NOT NULL\n)");
                    table.CheckConstraint("ck_payment_webhook_events_status", "processing_status IN\n(\n    'Processing',\n    'Processed',\n    'Failed'\n)");
                });

            migrationBuilder.CreateIndex(
                name: "ux_payment_webhook_events_event_id",
                table: "payment_webhook_events",
                column: "event_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_webhook_events");
        }
    }
}
