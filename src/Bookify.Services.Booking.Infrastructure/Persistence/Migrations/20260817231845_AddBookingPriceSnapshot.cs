using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Services.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingPriceSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "booking_price_snapshots",
                columns: table => new
                {
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accommodation_price_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    accommodation_price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    extra_guest_price_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    extra_guest_price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    total_price_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    total_price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_price_snapshots", x => x.booking_id);
                    table.CheckConstraint("ck_booking_price_snapshots_amounts", "accommodation_price_amount >= 0 AND extra_guest_price_amount >= 0 AND total_price_amount >= 0");
                    table.CheckConstraint("ck_booking_price_snapshots_currencies", "accommodation_price_currency = extra_guest_price_currency AND accommodation_price_currency = total_price_currency");
                    table.CheckConstraint("ck_booking_price_snapshots_currency_format", "accommodation_price_currency ~ '^[A-Z]{3}$' AND extra_guest_price_currency ~ '^[A-Z]{3}$' AND total_price_currency ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_booking_price_snapshots_total", "total_price_amount = accommodation_price_amount + extra_guest_price_amount");
                    table.ForeignKey(
                        name: "FK_booking_price_snapshots_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_price_snapshots");
        }
    }
}
