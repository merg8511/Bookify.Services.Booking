
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.Services;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.Immutable;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Infrastructure.DemoSeeding;

public sealed class AvailabilityConflictsDemoSeedBatch : IDemoSeedBatch
{
    public const string BatchKey = "23.6.availability-conficts.v1";
    public const string RoomConflictEmail = "room-conflict-23-6@bookify.invalid";
    public const string EntirePropertyConflictEmail = "entire-conflict-23-6@bookify.invalid";
    public const string CancelledEmail = "cancelled-23-6@bookify.invalid";
    public const string SeasonalPriceEmail = "seasonal-price-23-6@bookify.invalid";

    private readonly IClock _clock;

    public AvailabilityConflictsDemoSeedBatch(IClock clock)
    {
        _clock = clock ??
            throw new ArgumentNullException(nameof(clock));
    }

    public string Key => BatchKey;

    public async Task ExecuteAsync(BookingDbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        cancellationToken.ThrowIfCancellationRequested();

        DomainBooking[] anchors = await dbContext
            .Bookings
            .AsNoTracking()
            .Where(booking => booking.GuestDetails != null &&
                booking.GuestDetails.Email == BookingStatesDemoSeedBatch.PaidDemoEmail)
            .Take(2)
            .ToArrayAsync(cancellationToken);

        if (anchors.Length != 1)
        {
            throw new InvalidOperationException(
                "Availability demo seed requires exactly one Paid booking from module 23.3.");
        }

        DomainBooking anchor = anchors[0];

        if (anchor.Status is not (BookingStatus.Paid or BookingStatus.Completed))
        {
            throw new InvalidOperationException(
                "The reference booking must be Paid or Completed.");
        }

        Property? property = await dbContext
            .Properties
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == anchor.PropertyId, cancellationToken);

        if (property is null || !property.IsActive)
        {
            throw new InvalidOperationException("The demo Property is missing or inactive.");
        }

        RentableUnit[] units = await dbContext
            .RentableUnits
            .AsNoTracking()
            .Where(unit => unit.PropertyId == property.Id)
            .Include(unit => unit.Pricing)
            .Include(unit => unit.PricingSeasons)
            .ToArrayAsync(cancellationToken);

        RentableUnit[] rooms = units
            .Where(unit => unit.Type == RentableUnitType.Room &&
                   unit.IsActive &&
                   unit.Pricing is not null)
            .OrderBy(unit => unit.Id)
            .ToArray();

        if (rooms.Length < 3)
        {
            throw new InvalidOperationException(
                "Availability demo seed requires at least three active Rooms with configured pricing.");
        }

        RentableUnit[] entireUnits = units
            .Where(unit => unit.Type == RentableUnitType.EntireProperty &&
                   unit.IsActive &&
                   unit.Pricing is not null)
            .ToArray();

        if (entireUnits.Length != 1)
        {
            throw new InvalidOperationException(
                "Availability demo seed requires exactly one active EntireProperty with pricing.");
        }

        RentableUnit entireProperty = entireUnits[0];
        RentableUnit? seasonalRoom = rooms.FirstOrDefault(unit => unit.Id == anchor.RentableUnitId);

        if (seasonalRoom is null)
        {
            throw new InvalidOperationException("The reference booking does not belong to an active demo Room.");
        }

        RentableUnit[] otherRooms = rooms
            .Where(unit => unit.Id != seasonalRoom.Id)
            .Take(2)
            .ToArray();

        if (otherRooms.Length != 2)
        {
            throw new InvalidOperationException("Two additional demo Rooms are required.");
        }

        RentableUnit blockedRoom = otherRooms[0];
        RentableUnit cancelledRoom = otherRooms[1];

        DomainBooking[] existingBookings = await dbContext
            .Bookings
            .AsNoTracking()
            .Where(booking => booking.PropertyId == property.Id)
            .ToArrayAsync(cancellationToken);

        Dictionary<Guid, RentableUnit> unitsById = units.ToDictionary(unit => unit.Id);

        DateTimeOffset now = _clock.UtcNow.ToUniversalTime();
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            now, TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId)).DateTime);

        DateOnly latestCheckout = existingBookings
            .Select(booking => booking.StayPeriod.CheckOutDate)
            .DefaultIfEmpty(today)
            .Max();

        if (today.DayNumber > DateOnly.MaxValue.DayNumber - 120 ||
            latestCheckout.DayNumber > DateOnly.MaxValue.DayNumber - 32)
        {
            throw new InvalidOperationException(
                "The existing demo dates leave no safe future range for availability scenarios.");
        }

        DateOnly fromToday = today.AddDays(120);
        DateOnly afterExisting = latestCheckout.AddDays(14);
        DateOnly baseDate = fromToday > afterExisting ? fromToday : afterExisting;

        if (baseDate.DayNumber > DateOnly.MaxValue.DayNumber - 17)
        {
            throw new InvalidOperationException(
                "The calculated availability date range exceeds the supported calendar.");
        }

        DomainBooking roomConflict = CreateBooking(
            blockedRoom,
            baseDate,
            baseDate.AddDays(3),
            fullName: "Demo Conflicto Habitacion",
            email: RoomConflictEmail,
            createdAtUtc: now);

        DomainBooking entireConflict = CreateBooking(
            entireProperty,
            baseDate.AddDays(7),
            baseDate.AddDays(10),
            fullName: "Demo Conflicto Casa Completa",
            email: EntirePropertyConflictEmail,
            createdAtUtc: now);

        DomainBooking cancelled = CreateBooking(
            entireProperty,
            baseDate.AddDays(14),
            baseDate.AddDays(17),
            fullName: "Demo Cancelacion No Bloqueante",
            email: CancelledEmail,
            createdAtUtc: now.AddMinutes(-1));

        EnsureSuccess(
            cancelled.Cancel(now),
            "Cancel demo availability booking");

        PricingSeason[] specialSeasons = seasonalRoom
            .PricingSeasons
            .Where(season => season.IsActive && season.Priority == 20)
            .ToArray();

        if (specialSeasons.Length != 1)
        {
            throw new InvalidOperationException(
                "The demo Room must contain exactly one active special season with priority 20.");
        }

        PricingSeason specialSeason = specialSeasons[0];

        DateOnly seasonalCheckIn = specialSeason.StartDate;

        if (seasonalCheckIn <= today)
        {
            throw new InvalidOperationException(
                "The original demo special season is no longer in the future. The seed will not" +
                "create historical PendingApproval data.");
        }

        DateOnly seasonalCheckOut = seasonalCheckIn.AddDays(1);

        if (seasonalCheckOut > specialSeason.EndDate)
        {
            throw new InvalidOperationException(
                "The special season does not contain a complete night.");
        }

        DomainBooking seasonalBooking = CreateBooking(
            seasonalRoom,
            seasonalCheckIn,
            seasonalCheckOut,
            fullName: "Demo precio Temporada Especial",
            email: SeasonalPriceEmail,
            createdAtUtc: now);

        decimal expectedSeasonalRate = specialSeason.NightlyRate.Amount;

        if (seasonalBooking.PriceSnapshot is null ||
            seasonalBooking.PriceSnapshot.AccommodationPrice.Amount != expectedSeasonalRate)
        {
            throw new InvalidOperationException(
                "The seasonal demo booking did not resolve the expected priority-20 rate.");
        }

        DomainBooking[] candidates =
            [
                roomConflict,
                entireConflict,
                cancelled,
                seasonalBooking
            ];

        EnsureNoInventoryConflicts(candidates, existingBookings, unitsById);

        dbContext.Bookings.AddRange(candidates);
    }

    private static DomainBooking CreateBooking(
        RentableUnit unit,
        DateOnly checkInDate,
        DateOnly checkOutDate,
        string fullName,
        string email,
        DateTimeOffset createdAtUtc)
    {
        StayPeriod period = RequireSuccess(
            StayPeriod.Create(
                checkInDate,
                checkOutDate),
            "Create demo stay period");

        GuestCount guestCount = RequireSuccess(
            GuestCount.Create(2),
            "Create demo guest count");

        GuestDetails guestDetails = RequireSuccess(
            GuestDetails.Create(
                fullName,
                email,
                "+12025550107"),
            "Create fictional guest data");

        if (unit.Pricing is null)
        {
            throw new InvalidOperationException($"Missing pricing for demo unit '{unit.Id}'.");
        }

        PriceBreakdown breakDown = RequireSuccess(
            BookingPricingEngine.CalculatePrice(
                unit.Pricing,
                unit.MaxBaseGuests,
                guestCount,
                period,
                unit.PricingSeasons),
            "Calculate demo booking price");

        PriceSnapshot snapshot = PriceSnapshot.Create(breakDown);

        return RequireSuccess(
            DomainBooking.Create(
                unit,
                period,
                guestCount,
                guestDetails,
                snapshot,
                createdAtUtc,
                approvalDueAtUtc: createdAtUtc.AddDays(365)),
            "Create demo availability booking");
    }

    private static void EnsureNoInventoryConflicts(
        DomainBooking[] candidates,
        IReadOnlyList<DomainBooking> existingBookigs,
        Dictionary<Guid, RentableUnit> unitsById)
    {
        foreach (DomainBooking candidate in candidates)
        {
            if (!candidate.BlocksInventory)
            {
                continue;
            }

            RentableUnit candidateUnit = unitsById[candidate.RentableUnitId];

            foreach (DomainBooking existing in existingBookigs)
            {
                if (!existing.BlocksInventory ||
                    !candidate.StayPeriod.Overlaps(existing.StayPeriod))
                {
                    continue;
                }

                if (!unitsById.TryGetValue(existing.RentableUnitId, out RentableUnit? existingUnit))
                {
                    throw new InvalidOperationException(
                        "An existing blocking booking references an unknown RentableUnit.");
                }

                if (candidateUnit.SharesInventoryWith(existingUnit))
                {
                    throw new InvalidOperationException(
                        $"Availability demo seed conflicts with existing booking '{existing.Id}'.");
                }
            }
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            DomainBooking first = candidates[i];

            if (!first.BlocksInventory)
            {
                continue;
            }

            for (int j = i + 1; j < candidates.Length; j++)
            {
                DomainBooking second = candidates[j];

                if (!second.BlocksInventory ||
                    !first.StayPeriod.Overlaps(second.StayPeriod))
                {
                    continue;
                }

                RentableUnit firstUnit = unitsById[first.RentableUnitId];
                RentableUnit secondUnit = unitsById[second.RentableUnitId];

                if (firstUnit.SharesInventoryWith(secondUnit))
                {
                    throw new InvalidOperationException(
                        "Availability demo seed contains overlapping inventory blockers.");
                }
            }
        }
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

    private static void EnsureSuccess(Result result, string operation)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Demo seed failed during '{operation}': {result.Error}");
        }
    }
}
