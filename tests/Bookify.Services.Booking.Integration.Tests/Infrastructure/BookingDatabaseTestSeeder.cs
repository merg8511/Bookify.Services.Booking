using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Infrastructure;

internal sealed record SeedData(
    Guid PropertyId,
    Guid RentableUnitId,
    Property Property,
    RentableUnit RentableUnit);

internal static class BookingDatabaseTestSeeder
{
    public static async Task<SeedData> SeedPropertyWithRoomAsync(
        IServiceProvider serviceProvider,
        string? propertyName = null,
        string? roomName = null,
        string ownerSubjectId = TestIdentitySubjects.Owner,
        decimal weekdayPrice = 100m,
        decimal weekendPrice = 140m,
        decimal extraGuestPrice = 25m,
        int capacity = 4,
        int maxBaseGuests = 2,
        Action<RentableUnit>? configureUnit = null,
        CancellationToken cancellationToken = default)
    {
        var property = Property.Create(
            propertyName ?? $"Test Property {Guid.NewGuid():N}",
            "America/El_Salvador",
            new TimeOnly(15, 0),
            new TimeOnly(11, 0),
            ownerSubjectId).Value;

        var rentableUnit = RentableUnit.Create(
            property.Id,
            roomName ?? $"Room {Guid.NewGuid():N}",
            RentableUnitType.Room,
            maximumCapacity: capacity,
            maxBaseGuests: maxBaseGuests).Value;

        rentableUnit.ConfigurePricing(
            RentableUnitPricing.Create(
                Money.Create(weekdayPrice, "USD").Value,
                Money.Create(weekendPrice, "USD").Value,
                Money.Create(extraGuestPrice, "USD").Value).Value);

        configureUnit?.Invoke(rentableUnit);

        using IServiceScope scope = serviceProvider.CreateScope();
        IPropertyRepository propertyRepository = scope.ServiceProvider.GetRequiredService<IPropertyRepository>();
        IRentableUnitRepository rentableUnitRepository = scope.ServiceProvider.GetRequiredService<IRentableUnitRepository>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        propertyRepository.Add(property);
        rentableUnitRepository.Add(rentableUnit);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SeedData(property.Id, rentableUnit.Id, property, rentableUnit);
    }

    public static Task<SeedData> SeedPropertyWithRoomAsync(
        BookingApiFactory factory,
        string? propertyName = null,
        string? roomName = null,
        string ownerSubjectId = TestIdentitySubjects.Owner,
        decimal weekdayPrice = 100m,
        decimal weekendPrice = 140m,
        decimal extraGuestPrice = 25m,
        int capacity = 4,
        int maxBaseGuests = 2,
        Action<RentableUnit>? configureUnit = null,
        CancellationToken cancellationToken = default)
    {
        return SeedPropertyWithRoomAsync(
            factory.Services,
            propertyName,
            roomName,
            ownerSubjectId,
            weekdayPrice,
            weekendPrice,
            extraGuestPrice,
            capacity,
            maxBaseGuests,
            configureUnit,
            cancellationToken);
    }

    public static async Task<DomainBooking> SeedBookingAsync(
        IServiceProvider serviceProvider,
        BookingStatus status = BookingStatus.PendingApproval,
        BookingCancellationReason? cancellationReason = null,
        string? propertyName = null,
        StayPeriod? stayPeriod = null,
        GuestCount? guestCount = null,
        PriceSnapshot? priceSnapshot = null,
        string ownerSubjectId = TestIdentitySubjects.Owner,
        string? customerSubjectId = null,
        CancellationToken cancellationToken = default)
    {
        SeedData seed = await SeedPropertyWithRoomAsync(
            serviceProvider,
            propertyName: propertyName ?? $"Booking Test {Guid.NewGuid():N}",
            ownerSubjectId: ownerSubjectId,
            cancellationToken: cancellationToken);

        StayPeriod period = stayPeriod ?? StayPeriod.Create(
            new DateOnly(2026, 9, 10),
            new DateOnly(2026, 9, 12)).Value;

        DomainBooking booking = BookingTestData.CreateBooking(
            seed.RentableUnit,
            period,
            guestCount: guestCount,
            priceSnapshot: priceSnapshot,
            customerSubjectId: customerSubjectId);

        if (status is BookingStatus.PendingPayment or BookingStatus.Paid or BookingStatus.Completed
            || (status is BookingStatus.Cancelled && cancellationReason == BookingCancellationReason.PaymentExpired))
        {
            booking.Approve(BookingTestTime.ApprovedAtUtc, BookingTestTime.PaymentDueAtUtc);
        }

        if (status is BookingStatus.Paid or BookingStatus.Completed)
        {
            booking.MarkAsPaid(BookingTestTime.PaidAtUtc);
        }

        if (status is BookingStatus.Completed)
        {
            booking.Complete(BookingTestTime.CompletedAtUtc);
        }

        if (status is BookingStatus.Cancelled)
        {
            if (cancellationReason == BookingCancellationReason.RejectedByOwner)
            {
                booking.Reject(BookingTestTime.CreatedAtUtc.AddHours(2));
            }
            else if (cancellationReason == BookingCancellationReason.PaymentExpired)
            {
                booking.ExpirePayment(BookingTestTime.CreatedAtUtc.AddHours(24));
            }
            else
            {
                booking.Cancel(BookingTestTime.CancelledAtUtc);
            }
        }

        using IServiceScope scope = serviceProvider.CreateScope();
        IBookingRepository bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        bookingRepository.Add(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return booking;
    }

    public static Task<DomainBooking> SeedBookingAsync(
        BookingApiFactory factory,
        BookingStatus status = BookingStatus.PendingApproval,
        BookingCancellationReason? cancellationReason = null,
        string? propertyName = null,
        StayPeriod? stayPeriod = null,
        GuestCount? guestCount = null,
        PriceSnapshot? priceSnapshot = null,
        string ownerSubjectId = TestIdentitySubjects.Owner,
        string? customerSubjectId = null,
        CancellationToken cancellationToken = default)
    {
        return SeedBookingAsync(
            factory.Services,
            status,
            cancellationReason,
            propertyName,
            stayPeriod,
            guestCount,
            priceSnapshot,
            ownerSubjectId,
            customerSubjectId,
            cancellationToken);
    }
}
