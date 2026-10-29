using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.Services;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Infrastructure.DemoSeeding;

public sealed class BookingStatesDemoSeedBatch : IDemoSeedBatch
{
    public const string BatchKey = "23.3.bookings.v1";
    public const string PaidDemoEmail = "paid-23-3@bookify.invalid";
    public const string CompletedDemoEmail = "completed-23-3@bookify.invalid";
    private const string DemoPropertyName = "Bookify Demo - Casa Costa Azul";
    private const string DemoTimeZoneId = "America/El_Salvador";

    private readonly IClock _clock;
    private readonly string _ownerSubjectId;
    private readonly Guid? _propertyId;

    public BookingStatesDemoSeedBatch(
        IClock clock,
        string? ownerSubjectId,
        string? propertyId)
    {
        _clock = clock ??
            throw new ArgumentNullException(nameof(clock));

        if (string.IsNullOrWhiteSpace(ownerSubjectId) ||
           ownerSubjectId.Length > 255 ||
           !string.Equals(ownerSubjectId, ownerSubjectId.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "DemoSeed:OwnerSubjectId must contain a valir Owner subject identifier.");
        }

        _ownerSubjectId = ownerSubjectId;

        if (string.IsNullOrWhiteSpace(propertyId))
        {
            _propertyId = null;
        }
        else if (Guid.TryParse(propertyId.Trim(), out Guid parseId) &&
                parseId != Guid.Empty)
        {
            _propertyId = parseId;
        }
        else
        {
            throw new InvalidOperationException("DemoSeed:PropertyId must be a valid GUID.");
        }
    }

    public string Key => BatchKey;

    public async Task ExecuteAsync(BookingDbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        cancellationToken.ThrowIfCancellationRequested();

        Property property = await ResolvePropertyAsync(dbContext, cancellationToken);

        RentableUnit[] units = await dbContext
            .RentableUnits
            .AsNoTracking()
            .Where(unit => unit.PropertyId == property.Id)
            .Include(unit => unit.Pricing)
            .Include(unit => unit.PricingSeasons)
            .OrderBy(unit => unit.Id)
            .ToArrayAsync(cancellationToken);

        RentableUnit[] rooms = units
            .Where(unit =>
                unit.Type == RentableUnitType.Room &&
                unit.IsActive &&
                unit.Pricing is not null)
            .ToArray();

        if (rooms.Length < 3)
        {
            throw new InvalidOperationException(
                "Demo booking seed requires at least three active rooms with configured pricing. " +
                "Run the 23.2 property seed first.");
        }

        if (!units.Any(unit => unit.Type == RentableUnitType.EntireProperty))
        {
            throw new InvalidOperationException(
                "Demo booking seed requires an EntireProperty unnit created by the 23.2 seed.");
        }

        DateTimeOffset now = _clock.UtcNow.ToUniversalTime();
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime
            (now, TimeZoneInfo.FindSystemTimeZoneById(DemoTimeZoneId)).DateTime);

        DomainBooking pendingApproval = CreateBooking(
            rooms[0],
            today.AddDays(35),
            today.AddDays(38),
            guestCount: 2,
            fullName: "Demo Pendiente Aprobacion",
            email: "pending-approval-23-3@bookify.invalid",
            phone: "+50370000001",
            createdAtUtc: now,
            approvalDueAtUtc: now.AddDays(28));

        DomainBooking pendingPayment = CreateBooking(
            rooms[1],
            today.AddDays(42),
            today.AddDays(45),
            guestCount: 2,
            fullName: "Demo Pendiente Pago",
            email: "pending-payment-23-3@bookify.invalid",
            phone: "+50370000002",
            createdAtUtc: now.AddHours(-2),
            approvalDueAtUtc: now.AddDays(28));

        EnsureSuccess(pendingPayment
            .Approve(
                approvedAtUtc: now.AddHours(-1),
                paymentDueAtUtc: now.AddDays(28)),
            "Approve PendingPayment demo booking");

        DomainBooking paid = CreateBooking(
            rooms[2],
            today.AddDays(49),
            today.AddDays(52),
            guestCount: 2,
            fullName: "Demo Reserva Pagada",
            email: PaidDemoEmail,
            phone: "+50370000003",
            createdAtUtc: now.AddDays(-3),
            approvalDueAtUtc: now.AddDays(-2));

        EnsureSuccess(
            paid.Approve(
                approvedAtUtc: now.AddDays(-3).AddHours(1),
                paymentDueAtUtc: now.AddDays(-2)),
            "Approve Paid demo booking");

        Payment paidPayment = CreateSuccessfulPayment(
            paid,
            scenario: "paid",
            paymentCreatedAtUtc: now.AddDays(-3).AddHours(2),
            succeededAtUtc: now.AddDays(-3).AddHours(3));

        DomainBooking completed = CreateBooking(
           rooms[0],
           today.AddDays(-20),
           today.AddDays(-17),
           guestCount: 2,
           fullName: "Demo Estancia Completada",
           email: CompletedDemoEmail,
           phone: "+50370000004",
           createdAtUtc: now.AddDays(-40),
           approvalDueAtUtc: now.AddDays(-39));

        EnsureSuccess(
            completed.Approve(
                approvedAtUtc: now.AddDays(-40).AddHours(1),
                paymentDueAtUtc: now.AddDays(-39)),
            "Approve Completed demo booking");

        Payment completedPayment = CreateSuccessfulPayment(
            completed,
            scenario: "completed",
            paymentCreatedAtUtc: now.AddDays(-40).AddHours(2),
            succeededAtUtc: now.AddDays(-40).AddHours(3));

        EnsureSuccess(completed.Complete(now.AddDays(-16)), "Complete historical demo booking");

        DomainBooking cancelled = CreateBooking(
            rooms[1],
            today.AddDays(60),
            today.AddDays(63),
            guestCount: 2,
            fullName: "Demo Reserva Cancelada",
            email: "cancelled-23-3@bookify.invalid",
            phone: "+50370000005",
            createdAtUtc: now.AddDays(-1),
            approvalDueAtUtc: now.AddDays(27));

        EnsureSuccess(cancelled.Cancel(now.AddDays(-1).AddHours(1)), "Cancel demo booking");

        DomainBooking[] bookings =
        [
            pendingApproval,
            pendingPayment,
            paid,
            completed,
            cancelled
        ];

        DomainBooking[] existingBookings = await dbContext
            .Bookings
            .AsNoTracking()
            .Where(booking => booking.PropertyId == property.Id)
            .ToArrayAsync(cancellationToken);

        EnsureNoInventoryConflicts(bookings, existingBookings, units);

        dbContext.Bookings.AddRange(bookings);
        dbContext.Payments.AddRange(paidPayment, completedPayment);
    }

    private async Task<Property> ResolvePropertyAsync(BookingDbContext dbContext, CancellationToken cancellationToken)
    {
        IQueryable<Property> query = dbContext
            .Properties
            .AsNoTracking()
            .Where(property => property.OwnerSubjectId == _ownerSubjectId);

        if (_propertyId.HasValue)
        {
            query = query.Where(property => property.Id == _propertyId.Value);
        }
        else
        {
            query = query.Where(property => property.Name == DemoPropertyName);
        }

        Property[] matches = await query.Take(2).ToArrayAsync(cancellationToken);

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "Could not resolve a unique demo property. Verify DemoSeed:OwnerSubjectId and configure " +
                "DemoSeed:PropertyId with the GUID created in 23.2.");
        }

        if (!matches[0].IsActive)
        {
            throw new InvalidOperationException("The demo property must be active.");
        }

        return matches[0];
    }

    private static DomainBooking CreateBooking(
        RentableUnit unit,
        DateOnly checkInDate,
        DateOnly checkOutDate,
        int guestCount,
        string fullName,
        string email,
        string phone,
        DateTimeOffset createdAtUtc,
        DateTimeOffset approvalDueAtUtc)
    {
        StayPeriod stayPeriod = RequireSuccess(
            StayPeriod.Create(
                checkInDate,
                checkOutDate),
            "Create stay period");

        GuestCount guests = RequireSuccess(
            GuestCount.Create(guestCount),
            "Create guest count");

        GuestDetails guestDetails = RequireSuccess(
            GuestDetails.Create(
                fullName,
                email,
                phone),
            "Create demo guest details");

        if (unit.Pricing is null)
        {
            throw new InvalidOperationException($"Pricing is missing for unit '{unit.Id}'.");
        }

        PriceBreakdown breakdown = RequireSuccess(
            BookingPricingEngine.CalculatePrice(
                unit.Pricing,
                unit.MaxBaseGuests,
                guests,
                stayPeriod,
                unit.PricingSeasons),
            "Calculate demo booking price");

        PriceSnapshot snapshot = PriceSnapshot.Create(breakdown);

        return RequireSuccess(
            DomainBooking.Create(
                unit,
                stayPeriod,
                guests,
                guestDetails,
                snapshot,
                createdAtUtc,
                approvalDueAtUtc),
            "Create demo booking");
    }

    private static Payment CreateSuccessfulPayment(
        DomainBooking booking,
        string scenario,
        DateTimeOffset paymentCreatedAtUtc,
        DateTimeOffset succeededAtUtc)
    {
        if (booking.PriceSnapshot is null)
        {
            throw new InvalidOperationException(
                "A paid demo booking requires a price snapshot.");
        }

        Payment payment = RequireSuccess(
            Payment.Create(
                booking.Id,
                booking.PriceSnapshot.TotalPrice,
                paymentCreatedAtUtc),
            $"Create {scenario} payment");

        string idempotencyKey = $"bookify-demo-23.3-{scenario}-v1";
        string externalReference = $"fake-demo-23.3-{scenario}-v1";

        RequireSuccess(
            payment.AddAttempt(
                idempotencyKey,
                externalReference,
                paymentCreatedAtUtc.AddMinutes(1)),
            $"Create {scenario} payment attempt");

        EnsureSuccess(
            payment.MarkAttemptAsSucceeded(
                externalReference,
                succeededAtUtc),
            $"Confirm {scenario} payment");

        EnsureSuccess(
            booking.MarkAsPaid(
                succeededAtUtc),
            $"Mark {scenario} booking as Paid");

        return payment;
    }

    private static void EnsureNoInventoryConflicts(
        DomainBooking[] candidates,
        IReadOnlyList<DomainBooking> existingBookings,
        IReadOnlyList<RentableUnit> propertyUnits)
    {
        Dictionary<Guid, RentableUnit> unitsById = propertyUnits.ToDictionary(unit => unit.Id);

        foreach (DomainBooking candidate in candidates)
        {
            if (!candidate.BlocksInventory)
            {
                continue;
            }

            RentableUnit candidateUnit = unitsById[candidate.RentableUnitId];

            foreach (DomainBooking existing in existingBookings)
            {
                if (!existing.BlocksInventory ||
                   !candidate.StayPeriod.Overlaps(existing.StayPeriod))
                {
                    continue;
                }

                if (!unitsById.TryGetValue(existing.RentableUnitId, out RentableUnit? existingUnit))
                {
                    throw new InvalidOperationException(
                        "An existing booking references an unknow rentable unit.");
                }

                if (candidateUnit.SharesInventoryWith(existingUnit))
                {
                    throw new InvalidOperationException(
                        $"Demo booking seed detected an inventory conflict with existing booking {existing.Id}");
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
                        "Demo booking seed contains conflicting booking scenarios.");
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
