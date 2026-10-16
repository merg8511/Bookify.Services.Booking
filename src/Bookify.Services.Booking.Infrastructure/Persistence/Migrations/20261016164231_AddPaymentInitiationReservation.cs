using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Services.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentInitiationReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "pending_initiation_created_at_utc",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pending_initiation_key",
                table: "payments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_payments_initiation_reservation_consistency",
                table: "payments",
                sql: "(pending_initiation_key IS NULL AND pending_initiation_created_at_utc IS NULL) or(pending_initiation_key IS NOT NULL AND pending_initiation_created_at_utc IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_payments_initiation_reservation_consistency",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "pending_initiation_created_at_utc",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "pending_initiation_key",
                table: "payments");
        }
    }
}
