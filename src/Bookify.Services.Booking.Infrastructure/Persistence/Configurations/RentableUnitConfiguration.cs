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
            rentableUnit => rentableUnit.IsEntireProperty);

        builder.OwnsOne(
            rentableUnit => rentableUnit.Pricing,
            pricingBuilder =>
            {
                pricingBuilder.ToTable(
                    "rentable_unit_pricing",
                    tableBuilder =>
                    {
                        tableBuilder.HasCheckConstraint(
                            "ck_rentable_unit_pricing_amounts",
                            "regular_nightly_rate_amount >= 0 " +
                            "AND weekend_nightly_rate_amount >= 0 " +
                            "AND extra_guest_nightly_rate_amount >= 0");

                        tableBuilder.HasCheckConstraint(
                            "ck_rentable_unit_pricing_currencies",
                            "regular_nightly_rate_currency = " +
                            "weekend_nightly_rate_currency " +
                            "AND regular_nightly_rate_currency = " +
                            "extra_guest_nightly_rate_currency");

                        tableBuilder.HasCheckConstraint(
                            "ck_rentable_unit_pricing_currency_format",
                            "regular_nightly_rate_currency ~ '^[A-Z]{3}$' " +
                            "AND weekend_nightly_rate_currency ~ '^[A-Z]{3}$' " +
                            "AND extra_guest_nightly_rate_currency ~ '^[A-Z]{3}$'");
                    });

                pricingBuilder
                    .Property<Guid>("RentableUnitId")
                    .HasColumnName("rentable_unit_id")
                    .HasColumnType("uuid");

                pricingBuilder
                    .HasKey("RentableUnitId")
                    .HasName("pk_rentable_unit_pricing");

                pricingBuilder
                    .WithOwner()
                    .HasForeignKey("RentableUnitId");

                pricingBuilder.OwnsOne(
                    pricing =>
                        pricing.RegularNightlyRate,
                    moneyBuilder =>
                    {
                        moneyBuilder
                            .Property(money => money.Amount)
                            .HasColumnName("regular_nightly_rate_amount")
                            .HasPrecision(18, 3)
                            .IsRequired();

                        moneyBuilder
                            .Property(money => money.Currency)
                            .HasColumnName("regular_nightly_rate_currency")
                            .HasMaxLength(3)
                            .IsRequired();
                    });

                pricingBuilder
                    .Navigation(
                        pricing => pricing.RegularNightlyRate)
                    .IsRequired();

                pricingBuilder.OwnsOne(
                    pricing => pricing.WeekendNightlyRate,
                    moneyBuilder =>
                    {
                        moneyBuilder
                            .Property(money => money.Amount)
                            .HasColumnName("weekend_nightly_rate_amount")
                            .HasPrecision(18, 3)
                            .IsRequired();

                        moneyBuilder
                            .Property(money => money.Currency)
                            .HasColumnName("weekend_nightly_rate_currency")
                            .HasMaxLength(3)
                            .IsRequired();
                    });

                pricingBuilder.Navigation(
                        pricing => pricing.WeekendNightlyRate)
                    .IsRequired(); ;

                pricingBuilder.OwnsOne(
                    pricing => pricing.ExtraGuestNightlyRate,
                    moneyBuilder =>
                    {
                        moneyBuilder
                            .Property(money => money.Amount)
                            .HasColumnName("extra_guest_nightly_rate_amount")
                            .HasPrecision(18, 3)
                            .IsRequired();

                        moneyBuilder
                        .Property(money => money.Currency)
                        .HasColumnName("extra_guest_nightly_rate_currency")
                        .HasMaxLength(3)
                        .IsRequired();
                    });

                pricingBuilder.Navigation(
                        pricing => pricing.ExtraGuestNightlyRate)
                    .IsRequired();
            });

        builder.OwnsMany(
            rentableUnit =>
                rentableUnit.PricingSeasons,
            seasonBuilder =>
            {
                seasonBuilder.ToTable(
                    "rentable_unit_pricing_seasons",
                    tableBuilder =>
                    {
                        tableBuilder.HasCheckConstraint(
                            "ck_rentable_unit_pricing_seasons_date_range",
                            "end_date > start_date");

                        tableBuilder.HasCheckConstraint(
                            "ck_rentable_unit_pricing_seasons_priority",
                            "priority >= 0");

                        tableBuilder.HasCheckConstraint(
                            "ck_rentable_unit_pricing_seasons_currency_format",
                            "nightly_rate_currency ~ '^[A-Z]{3}$'");
                    });

                seasonBuilder.Property<Guid>("RentableUnitId")
                    .HasColumnName("rentable_unit_id")
                    .HasColumnType("uuid");

                seasonBuilder.Property<int>("Id")
                    .HasColumnName("id")
                    .ValueGeneratedOnAdd();

                seasonBuilder.HasKey(
                        "RentableUnitId",
                        "Id")
                    .HasName("pk_rentable_unit_pricing_seasons");

                seasonBuilder
                    .WithOwner()
                    .HasForeignKey("RentableUnitId");

                seasonBuilder.Property(
                        season => season.StartDate)
                    .HasColumnName("start_date")
                    .HasColumnType("date")
                    .IsRequired();

                seasonBuilder.Property(
                        season => season.EndDate)
                    .HasColumnName("end_date")
                    .HasColumnType("date")
                    .IsRequired();

                seasonBuilder.Property(
                        season => season.Priority)
                    .HasColumnName("priority")
                    .IsRequired();

                seasonBuilder.OwnsOne(
                    season =>
                        season.NightlyRate,
                    moneyBuilder =>
                    {
                        moneyBuilder.Property(
                                money => money.Amount)
                            .HasColumnName(
                                "nightly_rate_amount")
                            .HasPrecision(18, 3)
                            .IsRequired();

                        moneyBuilder.Property(
                                money => money.Currency)
                            .HasColumnName(
                                "nightly_rate_currency")
                            .HasMaxLength(3)
                            .IsRequired();
                    });

                seasonBuilder.Navigation(
                        season =>
                            season.NightlyRate)
                    .IsRequired();
            });

        builder.Navigation(
                rentableUnit =>
                    rentableUnit.PricingSeasons)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

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
