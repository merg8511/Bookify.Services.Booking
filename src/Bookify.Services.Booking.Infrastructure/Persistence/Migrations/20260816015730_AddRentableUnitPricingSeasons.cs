using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bookify.Services.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRentableUnitPricingSeasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rentable_unit_pricing_seasons",
                columns: table => new
                {
                    rentable_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    nightly_rate_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    nightly_rate_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rentable_unit_pricing_seasons", x => new { x.rentable_unit_id, x.id });
                    table.CheckConstraint("ck_rentable_unit_pricing_seasons_currency_format", "nightly_rate_currency ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_rentable_unit_pricing_seasons_date_range", "end_date > start_date");
                    table.CheckConstraint("ck_rentable_unit_pricing_seasons_priority", "priority >= 0");
                    table.ForeignKey(
                        name: "FK_rentable_unit_pricing_seasons_rentable_units_rentable_unit_~",
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
                name: "rentable_unit_pricing_seasons");
        }
    }
}
