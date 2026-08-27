using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Services.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniquePendingPaymentAttempt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_payment_attempts_payment_id_pending",
                table: "payment_attempts",
                column: "payment_id",
                unique: true,
                filter: "status = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_payment_attempts_payment_id_pending",
                table: "payment_attempts");
        }
    }
}
