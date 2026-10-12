using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Services.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingWorkSelectionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_bookings_paid_check_in_date",
                table: "bookings",
                column: "check_in_date",
                filter: "status = 'Paid'");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_paid_check_out_date",
                table: "bookings",
                column: "check_out_date",
                filter: "status = 'Paid'");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_pending_approval_due_at_utc",
                table: "bookings",
                columns: new[] { "approval_due_at_utc", "id" },
                filter: "status = 'PendingApproval' AND approval_due_at_utc IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_pending_payment_due_at_utc",
                table: "bookings",
                columns: new[] { "payment_due_at_utc", "id" },
                filter: "status = 'PendingPayment' AND payment_due_at_utc IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_bookings_paid_check_in_date",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "ix_bookings_paid_check_out_date",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "ix_bookings_pending_approval_due_at_utc",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "ix_bookings_pending_payment_due_at_utc",
                table: "bookings");
        }
    }
}
