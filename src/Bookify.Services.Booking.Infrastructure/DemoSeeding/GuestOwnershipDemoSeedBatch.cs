namespace Bookify.Services.Booking.Infrastructure.DemoSeeding;

using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.Services;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

public sealed class GuestOwnershipDemoSeedBatch : IDemoSeedBatch
{
    public const string BatchKey = "23.4.guest-ownership.v1";
    public const string CustomerDemoEmail = "customer-23-4@bookif.invalid";
    private const string DemoTimeZoneId = "America/El_Salvador";

    private readonly IClock _clock;
    private readonly string _ownerSubjectId;
    private readonly string _customerSubjectId;

    public GuestOwnershipDemoSeedBatch(
        IClock clock,
        string? ownerSubjectId,
        string? customerSubjectId)
    {
        _clock = clock ??
            throw new ArgumentNullException(nameof(clock));

        _ownerSubjectId = ValidateSubjectId(ownerSubjectId, "DemoSeed:OwnerSubjectId");

        _customerSubjectId = ValidateSubjectId(customerSubjectId, "DemoSeed:CustomerSubjectId");
    }
    public string Key => BatchKey;

    public async Task ExecuteAsync(BookingDbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        cancellationToken.ThrowIfCancellationRequested();

        DomainBooking[] anchors = await dbContext
            .Bookings
            .AsNoTracking()
            .Where(booking => booking.GuestDetails != null
                && booking.GuestDetails.Email == BookingStatesDemoSeedBatch.PaidDemoEmail)
            .Take(2)
            .ToArrayAsync(cancellationToken);

        if (anchors.Length != 1)
        {
            throw new InvalidOperationException(
                "Guest ownership seed requires exactly one paid demo booking from module 23.3");
        }

        DomainBooking anchor = anchors[0];

        if (anchor.Status != BookingStatus.Paid ||
           anchor.CustomerSubjectId is not null ||
           string.IsNullOrWhiteSpace(anchor.GuestAccessTokenHash))
        {
            throw new InvalidOperationException(
                "The module 23.3 anchor booking does not have  the expected Guest ownership.");
        }

        Property? property = await dbContext
            .Properties
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == anchor.PropertyId, cancellationToken);

        if (property is null ||
           !property.IsActive ||
           !string.Equals(property.OwnerSubjectId, _ownerSubjectId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The demo Property is missing, inactive or does not belong to the configured Owner.");
        }

        RentableUnit? room = await dbContext
            .RentableUnits
            .AsNoTracking()
            .Include(unit => unit.Pricing)
            .Include(unit => unit.PricingSeasons)
            .SingleOrDefaultAsync(unit => unit.Id == anchor.RentableUnitId, cancellationToken);

        if (room is null ||
           room.PropertyId != property.Id ||
           room.Type != RentableUnitType.Room ||
           !room.IsActive ||
           room.Pricing is null)
        {
            throw new InvalidOperationException(
                "The demo Room is missing or not available for creating an authenticated booking.");
        }

        RentableUnit[] propertyUnits = await dbContext
            .RentableUnits
            .AsNoTracking()
            .Where(unit => unit.PropertyId == property.Id)
            .ToArrayAsync(cancellationToken);

        Dictionary<Guid, RentableUnit> unitsById = propertyUnits.ToDictionary(unit => unit.Id);

        DomainBooking[] existingBookings = await dbContext
            .Bookings
            .AsNoTracking()
            .Where(booking => booking.PropertyId == property.Id)
            .ToArrayAsync(cancellationToken);

        DateTimeOffset now = _clock.UtcNow.ToUniversalTime();
        DateTimeOffset localNow = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(DemoTimeZoneId));
        DateOnly today = DateOnly.FromDateTime(localNow.DateTime);
        DateOnly earliestFromToday = today.AddDays(90);
        DateOnly earliestFromAnchor = anchor.StayPeriod.CheckOutDate.AddDays(45);
        DateOnly earliestCheckIn = earliestFromToday > earliestFromAnchor ? earliestFromToday : earliestFromAnchor;

        StayPeriod stayPeriod = FindAvailableStayPeriod(
            room,
            earliestCheckIn,
            existingBookings,
            unitsById);

        GuestDetails guestDetails = RequireSuccess(
            GuestDetails.Create(
                "Cliente Demo Autenticado",
                CustomerDemoEmail,
                "+12025550106"),
            "Create fictional customer details");

        GuestCount guestCount = RequireSuccess(GuestCount.Create(2), "Create demo guest count");

        PriceBreakdown breakDown = RequireSuccess(
            BookingPricingEngine.CalculatePrice(
                room.Pricing,
                room.MaxBaseGuests,
                guestCount,
                stayPeriod,
                room.PricingSeasons),
            "Calculate customer booking price");

        PriceSnapshot snapshot = PriceSnapshot.Create(breakDown);

        DomainBooking booking = RequireSuccess(
            DomainBooking.Create(
                room,
                stayPeriod,
                guestCount,
                guestDetails,
                snapshot,
                createdAtUtc: now,
                approvalDueAtUtc: now.AddDays(28),
                customerSubjectId: _customerSubjectId),
            "Create authenticated customer booking");

        if (!string.Equals(booking.CustomerSubjectId, _customerSubjectId, StringComparison.Ordinal) ||
           booking.GuestAccessTokenHash is not null ||
           booking.TakeGuestAccessToken() is not null)
        {
            throw new InvalidOperationException(
                "Authenticated demo booking ownership is inconsistent.");
        }

        dbContext.Bookings.Add(booking);
    }

    private static StayPeriod FindAvailableStayPeriod(
        RentableUnit requestedUnit,
        DateOnly earliestCheckIn,
        IReadOnlyList<DomainBooking> existingBookings,
        Dictionary<Guid, RentableUnit> unitsById)
    {
        for (int offset = 0; offset < 90; offset++)
        {
            DateOnly checkIn = earliestCheckIn.AddDays(offset);

            StayPeriod proposedStay = RequireSuccess(
                StayPeriod.Create(
                    checkIn,
                    checkIn.AddDays(3)),
                "Create proposed demo stay");

            bool hasConflict = false;

            foreach (DomainBooking existing in existingBookings)
            {
                if (!existing.BlocksInventory ||
                   !proposedStay.Overlaps(existing.StayPeriod))
                {
                    continue;
                }

                if (!unitsById.TryGetValue(existing.RentableUnitId, out RentableUnit? existingUnit))
                {
                    throw new InvalidOperationException(
                        "A blocking demo booking reference a missing rentable unit.");
                }

                if (requestedUnit.SharesInventoryWith(existingUnit))
                {
                    hasConflict = true;
                    break;
                }
            }

            if (!hasConflict)
            {
                return proposedStay;
            }
        }

        throw new InvalidOperationException(
            "No available demo stay was found in the configured 90-day searcg window.");
    }

    private static string ValidateSubjectId(string? subjectId, string settingName)
    {
        if (string.IsNullOrWhiteSpace(subjectId) ||
           subjectId.Length > 255 ||
           !string.Equals(subjectId, subjectId.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{settingName} must contain a valid identity-provider subject identifier.");
        }

        return subjectId;
    }

    private static T RequireSuccess<T>(Result<T> result, string operation)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Demo seed failed during '{operation}': {result.Error}");
        }

        return result.Value;
    }
}
