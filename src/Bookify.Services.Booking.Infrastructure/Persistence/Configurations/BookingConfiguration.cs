using Bookify.Services.Booking.Domain.Properties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Configurations;

internal sealed class BookingConfiguration
    : IEntityTypeConfiguration<DomainBooking>
{
    public void Configure(EntityTypeBuilder<DomainBooking> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "bookings",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_bookings_guest_count",
                    "guest_count > 0");

                tableBuilder.HasCheckConstraint(
                    "ck_bookgins_stay_period",
                    "check_out_date > check_in_date");

                tableBuilder.HasCheckConstraint(
                    "ck_bookings_status",
                    "status IN ('PendingApproval', 'PendingPayment', " +
                    "'Paid', 'Completed', 'Cancelled')");

                tableBuilder.HasCheckConstraint(
                    "ck_bookings_cancellation_consistency",
                    "(status = 'Cancelled' AND cancellation_reason IS NOT NULL)" +
                    " OR (status <> 'Cancelled' AND cancellation_reason IS NULL)");
            });

        builder.HasKey(
                booking => booking.Id)
            .HasName(
                "pk_bookings");

        builder.Property(
                booking => booking.Id)
            .HasColumnName(
                "id")
            .HasColumnType(
                "uuid")
            .ValueGeneratedNever();

        builder.Property(
                booking => booking.PropertyId)
            .HasColumnName(
                "property_id")
            .HasColumnType(
                "uuid")
            .IsRequired();

        builder.Property(
                booking => booking.RentableUnitId)
            .HasColumnName(
                "rentable_unit_id")
            .HasColumnType(
                "uuid")
            .IsRequired();

        builder.Property(
                booking => booking.Status)
            .HasColumnName(
                "status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(
                booking => booking.CancellationReason)
            .HasColumnName(
                "cancellation_reason")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.OwnsOne(
            booking => booking.StayPeriod,
            stayPeriodBuilder =>
            {
                stayPeriodBuilder.Property(
                        stayPeriod =>
                            stayPeriod.CheckInDate)
                    .HasColumnName(
                        "check_in_date")
                    .HasColumnType(
                        "date")
                    .IsRequired();

                stayPeriodBuilder.Property(
                        stayPeriod =>
                            stayPeriod.CheckOutDate)
                    .HasColumnName(
                        "check_out_date")
                    .HasColumnType(
                        "date")
                    .IsRequired();

                stayPeriodBuilder.Ignore(
                    stayPeriod =>
                        stayPeriod.NumberOfNights);

                stayPeriodBuilder.HasIndex(
                        stayPeriod =>
                            new
                            {
                                stayPeriod.CheckInDate,
                                stayPeriod.CheckOutDate
                            })
                    .HasDatabaseName(
                        "ix_bookings_check_in_date_check_out_date");
            });

        builder.Navigation(
                booking =>
                    booking.StayPeriod)
            .IsRequired();

        builder.OwnsOne(
            booking => booking.GuestCount,
            guestCountBuilder =>
            {
                guestCountBuilder.Property(
                        guestCount =>
                            guestCount.Value)
                    .HasColumnName(
                        "guest_count")
                    .IsRequired();
            });

        builder.Navigation(
                booking =>
                    booking.GuestCount)
            .IsRequired();

        builder.Ignore(
            booking =>
                booking.BlocksInventory);

        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(
                booking =>
                    booking.PropertyId)
            .OnDelete(
                DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_bookings_properties_property_id");

        builder.HasOne<RentableUnit>()
            .WithMany()
            .HasForeignKey(
                booking =>
                    booking.RentableUnitId)
            .OnDelete(
                DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_bookings_rentable_units_rentable_unit_id");

        builder.HasIndex(
                booking =>
                    new
                    {
                        booking.PropertyId,
                        booking.Status
                    })
            .HasDatabaseName(
                "ix_bookings_property_id_status");

        builder.HasIndex(
                booking =>
                    new
                    {
                        booking.RentableUnitId,
                        booking.Status
                    })
            .HasDatabaseName(
                "ix_bookings_rentable_unit_id_status");
    }
}
