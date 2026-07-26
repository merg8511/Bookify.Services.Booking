using Bookify.Services.Booking.Domain.Properties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Configurations;

internal sealed class RentableUnitConfiguration
    : IEntityTypeConfiguration<RentableUnit>
{
    public void Configure(EntityTypeBuilder<RentableUnit> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("rentable_units",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_rentable_units_maximum_capacity",
                    "maximum_capacity > 0");

                tableBuilder.HasCheckConstraint(
                    "ck_rentable_units_base_guests_capacity",
                    "max_base_guests <= maximum_capacity");

                tableBuilder.HasCheckConstraint(
                    "ck_rentable_units_type",
                    "type IN ('EntireProperty', 'Room')");
            });

        builder.HasKey(
            rentableUnit => rentableUnit.Id)
                .HasName("pk_rentable_units");

        builder.Property(
            rentableUnit => rentableUnit.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .ValueGeneratedNever();

        builder.Property(
            rentableUnit => rentableUnit.PropertyId)
                .HasColumnName("property_id")
                .HasColumnType("uuid")
                .IsRequired();

        builder.Property(
            rentableUnit => rentableUnit.Name)
                .HasColumnName("name")
                .HasMaxLength(200)
                .IsRequired();

        builder.Property(
            rentableUnit => rentableUnit.Type)
                .HasColumnName("type")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

        builder.Property(
            rentableUnit => rentableUnit.MaximumCapacity)
                .HasColumnName("maximum_capacity")
                .IsRequired();

        builder.Property(
            rentableUnit => rentableUnit.MaxBaseGuests)
                .HasColumnName("max_base_guests")
                .IsRequired();

        builder.Property(
            rentableUnit => rentableUnit.IsActive)
                .HasColumnName("is_active")
                .IsRequired();

        builder.Ignore(
            rentableUnit => rentableUnit.isEntireProperty);

        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(
                rentableUnit => rentableUnit.PropertyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_rentable_units_properties_id");

        builder.HasIndex(
            rentableUnit =>
                new
                {
                    rentableUnit.PropertyId,
                    rentableUnit.Type
                })
            .HasDatabaseName(
                "ix_rentable_units_property_id_type");
    }
}
