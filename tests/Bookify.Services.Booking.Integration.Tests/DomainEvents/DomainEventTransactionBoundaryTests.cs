using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.DomainEvents;

[Collection(BookingApiTestFixture.Name)]
public sealed class DomainEventTransactionBoundaryTests
{
    private readonly BookingApiFactory _factory;

    public DomainEventTransactionBoundaryTests(BookingApiFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    [Fact]
    public async Task SaveChangesAsync_WithoutExplicitTransaction_ShouldClearDomainEventsAfterPersistence()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RentableUnit rentableUnit = await CreatePersistedRentableUnitAsync(cancellationToken);
        DomainBooking booking = CreateBooking(rentableUnit);

        using IServiceScope scope = _factory.Services.CreateScope();
        BookingDbContext dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        Assert.Single(booking.GetDomainEvents());
        dbContext.Bookings.Add(booking);

        // ACT
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // ASSERT
        Assert.Empty(booking.GetDomainEvents());

        bool bookingExists = await dbContext.Bookings
            .AnyAsync(current => current.Id == booking.Id, cancellationToken);

        Assert.True(bookingExists);
    }

    [Fact]
    public async Task SaveChangesAsync_WithExplicitTransaction_ShouldKeepEventsPendingUntilCommit()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RentableUnit rentableUnit = await CreatePersistedRentableUnitAsync(cancellationToken);
        DomainBooking booking = CreateBooking(rentableUnit);

        using IServiceScope scope = _factory.Services.CreateScope();
        BookingDbContext dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        ITransactionManager transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();

        await using ITransaction transaction = await transactionManager.BeginAsync(cancellationToken);
        dbContext.Bookings.Add(booking);

        // ACT
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // ASSERT
        Assert.Single(booking.GetDomainEvents());

        // ACT
        await transaction.CommitAsync(cancellationToken);

        // ASSERT
        Assert.Empty(booking.GetDomainEvents());

        Guid bookingId = booking.Id;

        using IServiceScope verificationScope = _factory.Services.CreateScope();
        BookingDbContext verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BookingDbContext>();

        bool bookingExists = await verificationDbContext.Bookings
            .AsNoTracking()
            .AnyAsync(current => current.Id == bookingId, cancellationToken);

        Assert.True(bookingExists);
    }

    [Fact]
    public async Task RollbackAsync_ShouldDiscardPendingDomainEventsAndDatabaseChanges()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RentableUnit rentableUnit = await CreatePersistedRentableUnitAsync(cancellationToken);
        DomainBooking booking = CreateBooking(rentableUnit);
        Guid bookingId = booking.Id;

        using IServiceScope scope = _factory.Services.CreateScope();
        BookingDbContext dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        ITransactionManager transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();

        await using ITransaction transaction = await transactionManager.BeginAsync(cancellationToken);
        dbContext.Bookings.Add(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        Assert.Single(booking.GetDomainEvents());

        // ACT
        await transaction.RollbackAsync(cancellationToken);

        // ASSERT
        Assert.Empty(booking.GetDomainEvents());

        using IServiceScope verificationScope = _factory.Services.CreateScope();
        BookingDbContext verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<BookingDbContext>();

        bool bookingExists = await verificationDbContext.Bookings
            .AsNoTracking()
            .AnyAsync(current => current.Id == bookingId, cancellationToken);

        Assert.False(bookingExists);
    }

    private async Task<RentableUnit> CreatePersistedRentableUnitAsync(CancellationToken cancellationToken)
    {
        SeedData seedData = await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
            _factory.Services,
            propertyName: "Domain event property",
            roomName: "Domain event unit",
            cancellationToken: cancellationToken);

        return seedData.RentableUnit;
    }

    private static DomainBooking CreateBooking(RentableUnit rentableUnit)
    {
        StayPeriod stayPeriod = StayPeriod.Create(
            new DateOnly(2026, 9, 10),
            new DateOnly(2026, 9, 12)).Value;

        return BookingTestData.CreateBooking(rentableUnit, stayPeriod);
    }
}
