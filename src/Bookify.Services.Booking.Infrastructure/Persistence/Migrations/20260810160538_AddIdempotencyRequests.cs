using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Services.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "idempotency_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    http_method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    endpoint = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: true),
                    response_body = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_requests", x => x.id);
                    table.CheckConstraint("ck_idempotency_requests_completion", "(\n    status = 'InProgress'\n    AND status_code IS NULL\n)\nOR\n(\n    status = 'Completed'\n    AND status_code IS NOT NULL\n)");
                    table.CheckConstraint("ck_idempotency_requests_expiration", "expires_at > created_at");
                    table.CheckConstraint("ck_idempotency_requests_status", "status IN\n(\n    'InProgress',\n    'Completed'\n)");
                    table.CheckConstraint("ck_idempotency_requests_status_code", "status_code IS NULL\nOR\n(\n    status_code >= 100\n    AND status_code <= 599\n)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_requests_expires_at",
                table: "idempotency_requests",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ux_idempotency_requests_scope_key",
                table: "idempotency_requests",
                columns: new[] { "http_method", "endpoint", "key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_requests");
        }
    }
}
