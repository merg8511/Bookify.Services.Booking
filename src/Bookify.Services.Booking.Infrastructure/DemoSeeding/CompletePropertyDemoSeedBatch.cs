using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Infrastructure.Persistence;

namespace Bookify.Services.Booking.Infrastructure.DemoSeeding;

public sealed class CompletePropertyDemoSeedBatch : IDemoSeedBatch
{
    public const string BatchKey = "23.2.property-complete.v1";
    private const string DemoTimeZoneId = "America/El_Salvador";
    private const string Currency = "USD";
    private readonly IClock _clock;
    private readonly string _ownerSubjectId;

    public CompletePropertyDemoSeedBatch(
        IClock clock,
        string? ownerSubjectId)
    {
        _clock = clock ??
            throw new ArgumentNullException(nameof(clock));

        if (string.IsNullOrWhiteSpace(ownerSubjectId) ||
            ownerSubjectId.Length > 255 ||
            !string.Equals(ownerSubjectId, ownerSubjectId.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "DemoSeed:OwnerSubjectId must contain a valid Owner subject identifier " +
                "when demo seed is enabled.");
        }

        _ownerSubjectId = ownerSubjectId;
    }

    public string Key => BatchKey;

    public Task ExecuteAsync(BookingDbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        cancellationToken.ThrowIfCancellationRequested();

        Property property = RequireSuccess(
            Property.Create(
                "Bookify Demo - Casa Costa Azul",
                DemoTimeZoneId,
                new TimeOnly(15, 0),
                new TimeOnly(11, 0),
                _ownerSubjectId),
            "Create demo property");

        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(DemoTimeZoneId);

        DateTimeOffset localNow = TimeZoneInfo.ConvertTime(_clock.UtcNow, timeZone);
        DateOnly seasonStart = DateOnly.FromDateTime(localNow.DateTime).AddDays(30);

        RentableUnit[] units =
            [
                CreateUnit(
                    property.Id,
                    "Habitación Coral",
                    RentableUnitType.Room,
                    maximumCapacity: 2,
                    maxBaseGuests: 2,
                    regularRate: 85m,
                    weekendRate: 105m,
                    extraGuestRate: 20,
                    seasonStart),

                CreateUnit(
                    property.Id,
                    "Habitación Arena",
                    RentableUnitType.Room,
                    maximumCapacity: 3,
                    maxBaseGuests: 2,
                    regularRate: 100m,
                    weekendRate: 120m,
                    extraGuestRate: 25m,
                    seasonStart),

                CreateUnit(
                    property.Id,
                    "Suite Oceano",
                    RentableUnitType.Room,
                    maximumCapacity: 4,
                    maxBaseGuests: 2,
                    regularRate: 160m,
                    weekendRate: 190m,
                    extraGuestRate: 30m,
                    seasonStart),

                CreateUnit(
                    property.Id,
                    "Casa Completa",
                    RentableUnitType.EntireProperty,
                    maximumCapacity: 9,
                    maxBaseGuests: 6,
                    regularRate: 350m,
                    weekendRate: 425m,
                    extraGuestRate: 35m,
                    seasonStart)
            ];

        dbContext.Properties.Add(property);
        dbContext.RentableUnits.AddRange(units);

        return Task.CompletedTask;
    }

    private static RentableUnit CreateUnit(
        Guid propertyId,
        string name,
        RentableUnitType type,
        int maximumCapacity,
        int maxBaseGuests,
        decimal regularRate,
        decimal weekendRate,
        decimal extraGuestRate,
        DateOnly seasonStart)
    {
        RentableUnit unit = RequireSuccess(
            RentableUnit.Create(
                propertyId,
                name,
                type,
                maximumCapacity,
                maxBaseGuests),
                $"Create rentable unit '{name}'");

        Money regularMoney = CreateMoney(regularRate);
        Money weekendMoney = CreateMoney(weekendRate);
        Money extraGuestMoney = CreateMoney(extraGuestRate);

        RentableUnitPricing pricing = RequireSuccess(
            RentableUnitPricing.Create(
                regularMoney,
                weekendMoney,
                extraGuestMoney),
            $"Configure pricing for '{name}'");

        unit.ConfigurePricing(pricing);

        AddSeason(
            unit,
            seasonStart,
            seasonStart.AddDays(30),
            regularRate * 1.25m,
            priority: 10);

        AddSeason(
            unit,
            seasonStart.AddDays(10),
            seasonStart.AddDays(17),
            regularRate * 1.50m,
            priority: 20);

        return unit;
    }

    private static void AddSeason(
        RentableUnit unit,
        DateOnly startDate,
        DateOnly endDate,
        decimal nightlyRate,
        int priority)
    {
        Money nightlyMoney = CreateMoney(nightlyRate);

        PricingSeason season = RequireSuccess(
            PricingSeason.Create(
                startDate,
                endDate,
                nightlyMoney,
                priority),
            $"Create pricing season for '{unit.Name}'");

        Result addResult = unit.AddManagedPricingSeason(season);

        if (addResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Cannot add season to '{unit.Name}': {addResult.Error}");
        }
    }

    private static Money CreateMoney(decimal amount)
    {
        return RequireSuccess(
            Money.Create(amount, Currency),
            $"Createe money amount {amount} {Currency}");
    }

    private static T RequireSuccess<T>(Result<T> result, string operation)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Demo seeding failed during '{operation}': '{result.Error}'");
        }

        return result.Value;
    }
}
