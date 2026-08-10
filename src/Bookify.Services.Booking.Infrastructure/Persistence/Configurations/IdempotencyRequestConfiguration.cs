using Bookify.Services.Booking.Infrastructure.Persistence.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRequestConfiguration :
    IEntityTypeConfiguration<IdempotencyRequest>
{
    public void Configure(EntityTypeBuilder<IdempotencyRequest> builder)
    {
        builder.ToTable("idempotency_requests");

        builder.HasKey(
            request => request.Id);

        builder.Property(request => request.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(request => request.Key)
            .HasColumnName("key")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(request => request.HttpMethod)
            .HasColumnName("http_method")
            .HasColumnType("character varying(16)")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(request => request.Endpoint)
            .HasColumnName("endpoint")
            .HasColumnType("character varying(512)")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(request => request.RequestHash)
            .HasColumnName("request_hash")
            .HasColumnType("character varying(128)")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(request => request.Status)
            .HasColumnName("status")
            .HasColumnType("character varying(32)")
            .HasMaxLength(32)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(request => request.StatusCode)
            .HasColumnName("status_code")
            .HasColumnType("integer");

        builder.Property(request => request.ResponseBody)
            .HasColumnName("response_body")
            .HasColumnType("text");

        builder.Property(request => request.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(request => request.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(
            request =>
                new
                {
                    request.HttpMethod,
                    request.Endpoint,
                    request.Key
                })
            .IsUnique()
            .HasDatabaseName("ux_idempotency_requests_scope_key");

        builder.HasIndex(request => request.ExpiresAt)
            .HasDatabaseName("ix_idempotency_requests_expires_at");

        builder.ToTable(
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_idempotency_requests_status",
                    """
                    status IN
                    (
                        'InProgress',
                        'Completed'
                    )
                    """);

                tableBuilder.HasCheckConstraint(
                    "ck_idempotency_requests_status_code",
                    """
                    status_code IS NULL
                    OR
                    (
                        status_code >= 100
                        AND status_code <= 599
                    )
                    """);

                tableBuilder.HasCheckConstraint(
                    "ck_idempotency_requests_expiration",
                    "expires_at > created_at");

                tableBuilder.HasCheckConstraint(
                    "ck_idempotency_requests_completion",
                    """
                    (
                        status = 'InProgress'
                        AND status_code IS NULL
                    )
                    OR
                    (
                        status = 'Completed'
                        AND status_code IS NOT NULL
                    )
                    """);
            });
    }
}
