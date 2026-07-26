using Bookify.Services.Booking.Domain.Properties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Configurations;

internal sealed class PropertyConfiguration
    : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("properties");

        builder.HasKey(
            property => property.Id)
                .HasName("pk_properties");

        builder.Property(
            property => property.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .ValueGeneratedNever();

        builder.Property(
            property => property.Name)
                .HasColumnName("name")
                .HasMaxLength(200)
                .IsRequired();

        builder.Property(
            property => property.TimeZoneId)
                .HasColumnName("time_zone_id")
                .HasMaxLength(100)
                .IsRequired();

        builder.Property(
            property => property.CheckInTime)
                .HasColumnName("check_in_time")
                .HasColumnType("time without time zone")
                .IsRequired();

        builder.Property(
            property => property.CheckOutTime)
                .HasColumnName("check_out_time")
                .HasColumnType("time without time zone")
                .IsRequired();

        builder.Property(
            property => property.IsActive)
                .HasColumnName("is_active")
                .IsRequired();
    }
}
