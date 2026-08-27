using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Services.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.CheckConstraint("ck_payments_amount", "amount > 0");
                    table.CheckConstraint("ck_payments_completion_consistency", "(status IN ('Succeeded', 'Cancelled') AND completed_at_utc IS NOT NULL) OR (status IN ('Pending', 'Failed') AND completed_at_utc IS NULL)");
                    table.CheckConstraint("ck_payments_status", "status IN ('Pending', 'Succeeded', 'Failed', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_payments_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_attempts", x => x.id);
                    table.CheckConstraint("ck_payment_attempts_amount", "amount > 0");
                    table.CheckConstraint("ck_payment_attempts_completion_consistency", "(status = 'Pending' AND completed_at_utc IS NULL) OR (status IN ('Succeeded', 'Failed', 'Cancelled') AND completed_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_payment_attempts_status", "status IN ('Pending', 'Succeeded', 'Failed', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_payment_attempts_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_attempts_external_reference",
                table: "payment_attempts",
                column: "external_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_attempts_idempotency_key",
                table: "payment_attempts",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_attempts_payment_id",
                table: "payment_attempts",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_booking_id",
                table: "payments",
                column: "booking_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_attempts");

            migrationBuilder.DropTable(
                name: "payments");
        }
    }
}
