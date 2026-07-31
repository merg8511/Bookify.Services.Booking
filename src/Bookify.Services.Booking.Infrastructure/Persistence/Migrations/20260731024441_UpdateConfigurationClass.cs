using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Services.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateConfigurationClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_bookgins_stay_period",
                table: "bookings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bookings_stay_period",
                table: "bookings",
                sql: "check_out_date > check_in_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_bookings_stay_period",
                table: "bookings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bookgins_stay_period",
                table: "bookings",
                sql: "check_out_date > check_in_date");
        }
    }
}
