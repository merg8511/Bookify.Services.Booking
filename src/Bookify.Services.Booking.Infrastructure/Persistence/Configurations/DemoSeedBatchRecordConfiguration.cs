using Bookify.Services.Booking.Infrastructure.DemoSeeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Configurations;

internal sealed class DemoSeedBatchRecordConfiguration : IEntityTypeConfiguration<DemoSeedBatchRecord>
{
    public void Configure(EntityTypeBuilder<DemoSeedBatchRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .ToTable("demo_seed_batches");

        builder
            .HasKey(record => record.Key)
            .HasName("pk_demo_seed_batches");

        builder
            .Property(record => record.Key)
            .HasColumnName("batch_key")
            .HasMaxLength(128)
            .IsRequired();

        builder
            .Property(record => record.AppliedAtUtc)
            .HasColumnName("applied_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
