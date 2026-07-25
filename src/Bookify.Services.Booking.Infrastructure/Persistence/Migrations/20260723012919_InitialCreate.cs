using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Services.Booking.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "properties",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                check_in_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                check_out_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_properties", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "rentable_units",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                property_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                maximum_capacity = table.Column<int>(type: "integer", nullable: false),
                max_base_guests = table.Column<int>(type: "integer", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_rentable_units", x => x.id);
                table.CheckConstraint("ck_rentable_units_base_guests_capacity", "max_base_guests <= maximum_capacity");
                table.CheckConstraint("ck_rentable_units_maximum_capacity", "maximum_capacity > 0");
                table.CheckConstraint("ck_rentable_units_type", "type IN ('EntireProperty', 'Room')");
                table.ForeignKey(
                    name: "fk_rentable_units_properties_id",
                    column: x => x.property_id,
                    principalTable: "properties",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "bookings",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                property_id = table.Column<Guid>(type: "uuid", nullable: false),
                rentable_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                check_in_date = table.Column<DateOnly>(type: "date", nullable: false),
                check_out_date = table.Column<DateOnly>(type: "date", nullable: false),
                guest_count = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                cancellation_reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_bookings", x => x.id);
                table.CheckConstraint("ck_bookgins_stay_period", "check_out_date > check_in_date");
                table.CheckConstraint("ck_bookings_cancellation_consistency", "(status = 'Cancelled' AND cancellation_reason IS NOT NULL) OR (status <> 'Cancelled' AND cancellation_reason IS NULL)");
                table.CheckConstraint("ck_bookings_guest_count", "guest_count > 0");
                table.CheckConstraint("ck_bookings_status", "status IN ('PendingApproval', 'PendingPayment', 'Paid', 'Completed', 'Cancelled')");
                table.ForeignKey(
                    name: "fk_bookings_properties_property_id",
                    column: x => x.property_id,
                    principalTable: "properties",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_bookings_rentable_units_rentable_unit_id",
                    column: x => x.rentable_unit_id,
                    principalTable: "rentable_units",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_bookings_check_in_date_check_out_date",
            table: "bookings",
            columns: new[] { "check_in_date", "check_out_date" });

        migrationBuilder.CreateIndex(
            name: "ix_bookings_property_id_status",
            table: "bookings",
            columns: new[] { "property_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_bookings_rentable_unit_id_status",
            table: "bookings",
            columns: new[] { "rentable_unit_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_rentable_units_property_id_type",
            table: "rentable_units",
            columns: new[] { "property_id", "type" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "bookings");

        migrationBuilder.DropTable(
            name: "rentable_units");

        migrationBuilder.DropTable(
            name: "properties");
    }
}
