using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Services.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScopeIdempotencyByCaller : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_idempotency_requests_scope_key",
                table: "idempotency_requests");

            migrationBuilder.AddColumn<string>(
                name: "caller_scope",
                table: "idempotency_requests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ux_idempotency_requests_scope_key",
                table: "idempotency_requests",
                columns: new[] { "caller_scope", "http_method", "endpoint", "key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_idempotency_requests_scope_key",
                table: "idempotency_requests");

            migrationBuilder.DropColumn(
                name: "caller_scope",
                table: "idempotency_requests");

            migrationBuilder.CreateIndex(
                name: "ux_idempotency_requests_scope_key",
                table: "idempotency_requests",
                columns: new[] { "http_method", "endpoint", "key" },
                unique: true);
        }
    }
}
