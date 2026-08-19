using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Services.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRentableUnitPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rentable_unit_pricing",
                columns: table => new
                {
                    rentable_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    regular_nightly_rate_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    regular_nightly_rate_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    weekend_nightly_rate_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    weekend_nightly_rate_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    extra_guest_nightly_rate_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    extra_guest_nightly_rate_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rentable_unit_pricing", x => x.rentable_unit_id);
                    table.CheckConstraint("ck_rentable_unit_pricing_amounts", "regular_nightly_rate_amount >= 0 AND weekend_nightly_rate_amount >= 0 AND extra_guest_nightly_rate_amount >= 0");
                    table.CheckConstraint("ck_rentable_unit_pricing_currencies", "regular_nightly_rate_currency = weekend_nightly_rate_currency AND regular_nightly_rate_currency = extra_guest_nightly_rate_currency");
                    table.CheckConstraint("ck_rentable_unit_pricing_currency_format", "regular_nightly_rate_currency ~ '^[A-Z]{3}$' AND weekend_nightly_rate_currency ~ '^[A-Z]{3}$' AND extra_guest_nightly_rate_currency ~ '^[A-Z]{3}$'");
                    table.ForeignKey(
                        name: "FK_rentable_unit_pricing_rentable_units_rentable_unit_id",
                        column: x => x.rentable_unit_id,
                        principalTable: "rentable_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rentable_unit_pricing");
        }
    }
}
