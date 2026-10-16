using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Services.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxRetryState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_pending_created_at_utc",
                table: "outbox_messages");

            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "failed_at_utc",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_failure_code",
                table: "outbox_messages",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at_utc",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_due",
                table: "outbox_messages",
                columns: new[] { "next_attempt_at_utc", "created_at_utc", "id" },
                filter: "processed_at_utc IS NULL AND failed_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending_created_at_utc",
                table: "outbox_messages",
                columns: new[] { "created_at_utc", "id" },
                filter: "processed_at_utc IS NULL AND failed_at_utc IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_message_due",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_pending_created_at_utc",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "attempt_count",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "failed_at_utc",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "last_failure_code",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "next_attempt_at_utc",
                table: "outbox_messages");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending_created_at_utc",
                table: "outbox_messages",
                columns: new[] { "created_at_utc", "id" },
                filter: "processed_at_utc IS NULL");
        }
    }
}
