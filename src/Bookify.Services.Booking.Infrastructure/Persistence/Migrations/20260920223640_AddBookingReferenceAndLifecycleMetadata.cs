using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Services.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingReferenceAndLifecycleMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "approval_due_at_utc",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "approved_at_utc",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "booking_reference",
                table: "bookings",
                type: "character varying(27)",
                maxLength: 27,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelled_at_utc",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "completed_at_utc",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at_utc",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "paid_at_utc",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "payment_due_at_utc",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE bookings
                SET booking_reference =
                    'BK-' ||
                    UPPER(SUBSTRING(md5(id::text) FROM 1 FOR 4)) || '-' ||
                    UPPER(SUBSTRING(md5(id::text) FROM 5 FOR 4)) || '-' ||
                    UPPER(SUBSTRING(md5(id::text) FROM 9 FOR 4)) || '-' ||
                    UPPER(SUBSTRING(md5(id::text) FROM 13 FOR 4)) || '-' ||
                    UPPER(SUBSTRING(md5(id::text) FROM 17 FOR 4))
                WHERE booking_reference IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "booking_reference",
                table: "bookings",
                type: "character varying(27)",
                maxLength: 27,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(27)",
                oldMaxLength: 27,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_bookings_booking_reference",
                table: "bookings",
                column: "booking_reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_bookings_booking_reference",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "approval_due_at_utc",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "approved_at_utc",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "booking_reference",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "cancelled_at_utc",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "completed_at_utc",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "created_at_utc",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "paid_at_utc",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "payment_due_at_utc",
                table: "bookings");
        }
    }
}
