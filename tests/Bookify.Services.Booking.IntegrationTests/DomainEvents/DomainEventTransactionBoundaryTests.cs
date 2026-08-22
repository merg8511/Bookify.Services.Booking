using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.IntegrationTests.DomainEvents;

[Collection(BookingApiTestFixture.Name)]
public sealed class DomainEventTransactionBoundaryTests
{
    private readonly BookingApiFactory _factory;

    public DomainEventTransactionBoundaryTests(
        BookingApiFactory factory)
    {
        _factory =
            factory ??
            throw new ArgumentNullException(
                nameof(factory));
    }

    [Fact]
    public async Task SaveChangesAsync_WithoutExplicitTransaction_ShouldClearDomainEventsAfterPersistence()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using IServiceScope scope =
            _factory.Services.CreateScope();

        BookingDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<
                    BookingDbContext>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        RentableUnit rentableUnit =
            await CreatePersistedRentableUnitAsync(
                dbContext,
                unitOfWork);

        DomainBooking booking =
            CreateBooking(
                rentableUnit);

        Assert.Single(
            booking.GetDomainEvents());

        dbContext.Bookings.Add(
            booking);

        // ACT
        await unitOfWork
            .SaveChangesAsync(cancellationToken);

        // ASSERT
        Assert.Empty(
            booking.GetDomainEvents());

        bool bookingExists =
            await dbContext.Bookings
                .AnyAsync(
                    current =>
                        current.Id ==
                        booking.Id, cancellationToken);

        Assert.True(
            bookingExists);
    }

    [Fact]
    public async Task SaveChangesAsync_WithExplicitTransaction_ShouldKeepEventsPendingUntilCommit()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using IServiceScope scope =
            _factory.Services.CreateScope();

        BookingDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<
                    BookingDbContext>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        ITransactionManager
            transactionManager =
                scope.ServiceProvider
                    .GetRequiredService<
                        ITransactionManager>();

        RentableUnit rentableUnit =
            await CreatePersistedRentableUnitAsync(
                dbContext,
                unitOfWork);

        DomainBooking booking =
            CreateBooking(
                rentableUnit);

        await using ITransaction transaction =
            await transactionManager
                .BeginAsync(cancellationToken);

        dbContext.Bookings.Add(
            booking);

        // ACT
        await unitOfWork
            .SaveChangesAsync(cancellationToken);

        // ASSERT
        Assert.Single(
            booking.GetDomainEvents());

        // ACT
        await transaction
            .CommitAsync(cancellationToken);

        // ASSERT
        Assert.Empty(
            booking.GetDomainEvents());

        Guid bookingId =
            booking.Id;

        using IServiceScope verificationScope =
            _factory.Services.CreateScope();

        BookingDbContext verificationDbContext =
            verificationScope.ServiceProvider
                .GetRequiredService<
                    BookingDbContext>();

        bool bookingExists =
            await verificationDbContext
                .Bookings
                .AsNoTracking()
                .AnyAsync(
                    current =>
                        current.Id ==
                        bookingId, cancellationToken);

        Assert.True(
            bookingExists);
    }

    [Fact]
    public async Task RollbackAsync_ShouldDiscardPendingDomainEventsAndDatabaseChanges()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using IServiceScope scope =
            _factory.Services.CreateScope();

        BookingDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<
                    BookingDbContext>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        ITransactionManager
            transactionManager =
                scope.ServiceProvider
                    .GetRequiredService<
                        ITransactionManager>();

        RentableUnit rentableUnit =
            await CreatePersistedRentableUnitAsync(
                dbContext,
                unitOfWork);

        DomainBooking booking =
            CreateBooking(
                rentableUnit);

        Guid bookingId =
            booking.Id;

        await using ITransaction transaction =
            await transactionManager
                .BeginAsync(cancellationToken);

        dbContext.Bookings.Add(
            booking);

        await unitOfWork
            .SaveChangesAsync(cancellationToken);

        Assert.Single(
            booking.GetDomainEvents());

        // ACT
        await transaction
            .RollbackAsync(cancellationToken);

        // ASSERT
        Assert.Empty(
            booking.GetDomainEvents());

        using IServiceScope verificationScope =
            _factory.Services.CreateScope();

        BookingDbContext verificationDbContext =
            verificationScope.ServiceProvider
                .GetRequiredService<
                    BookingDbContext>();

        bool bookingExists =
            await verificationDbContext
                .Bookings
                .AsNoTracking()
                .AnyAsync(
                    current =>
                        current.Id ==
                        bookingId, cancellationToken);

        Assert.False(
            bookingExists);
    }

    private static async Task<RentableUnit>
        CreatePersistedRentableUnitAsync(
            BookingDbContext dbContext,
            IUnitOfWork unitOfWork)
    {
        Result<Property> propertyResult =
            Property.Create(
                "Domain event property",
                "UTC",
                new TimeOnly(15, 0),
                new TimeOnly(11, 0));

        Assert.True(
            propertyResult.IsSuccess);

        Property property =
            propertyResult.Value;

        Result<RentableUnit> rentableUnitResult =
            RentableUnit.Create(
                property.Id,
                "Domain event unit",
                RentableUnitType.EntireProperty,
                maximumCapacity: 4,
                maxBaseGuests: 2);

        Assert.True(
            rentableUnitResult.IsSuccess);

        RentableUnit rentableUnit =
            rentableUnitResult.Value;

        dbContext.Properties.Add(
            property);

        dbContext.RentableUnits.Add(
            rentableUnit);

        await unitOfWork
            .SaveChangesAsync();

        return rentableUnit;
    }

    private static DomainBooking CreateBooking(
        RentableUnit rentableUnit)
    {
        Result<StayPeriod> stayPeriodResult =
            StayPeriod.Create(
                new DateOnly(2026, 9, 10),
                new DateOnly(2026, 9, 12));

        Assert.True(
            stayPeriodResult.IsSuccess);

        Result<GuestCount> guestCountResult =
            GuestCount.Create(2);

        Assert.True(
            guestCountResult.IsSuccess);

        Result<DomainBooking> bookingResult =
            DomainBooking.Create(
                rentableUnit,
                stayPeriodResult.Value,
                guestCountResult.Value);

        Assert.True(
            bookingResult.IsSuccess);

        return bookingResult.Value;
    }
}
