using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Services.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceOwnershipAndGuestAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "owner_subject_id",
                table: "properties",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "customer_subject_id",
                table: "bookings",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "guest_access_token_hash",
                table: "bookings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_properties_owner_subject_id",
                table: "properties",
                column: "owner_subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_customer_subject_id",
                table: "bookings",
                column: "customer_subject_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bookings_access_consistency",
                table: "bookings",
                sql: "(\n    customer_subject_id IS NOT NULL\n    AND guest_access_token_hash IS NULL\n)\nOR\n(\n    customer_subject_id IS NULL\n    AND guest_access_token_hash IS NOT NULL\n)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bookings_guest_access_token_hash",
                table: "bookings",
                sql: "guest_access_token_hash IS NULL\nOR guest_access_token_hash ~ '^[0-9A-F]{64}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_properties_owner_subject_id",
                table: "properties");

            migrationBuilder.DropIndex(
                name: "ix_bookings_customer_subject_id",
                table: "bookings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bookings_access_consistency",
                table: "bookings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bookings_guest_access_token_hash",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "owner_subject_id",
                table: "properties");

            migrationBuilder.DropColumn(
                name: "customer_subject_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "guest_access_token_hash",
                table: "bookings");
        }
    }
}
