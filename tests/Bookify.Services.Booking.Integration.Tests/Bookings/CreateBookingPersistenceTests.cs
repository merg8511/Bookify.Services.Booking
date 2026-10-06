using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class CreateBookingPersistenceTests
{
    private readonly BookingApiFactory _factory;

    public CreateBookingPersistenceTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExecuteAsync_WithAvailableUnit_PersistsBooking()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeedData data = await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
            _factory.Services,
            propertyName: "Persistence Test Property",
            cancellationToken: cancellationToken);

        Result<CreateBookingResult> creationResult;

        using (IServiceScope commandScope = _factory.Services.CreateScope())
        {
            var executor = commandScope.ServiceProvider
                .GetRequiredService<ICommandExecutor<CreateBookingCommand, CreateBookingResult>>();

            var command = new CreateBookingCommand(
                data.PropertyId,
                data.RentableUnitId,
                new DateOnly(2026, 8, 10),
                new DateOnly(2026, 8, 15),
                GuestCount: 2,
                GuestFullName: "John Doe",
                GuestEmail: "john@example.com",
                GuestPhone: "+50377778888");

            // ACT
            creationResult = await executor.ExecuteAsync(command, cancellationToken);
        }

        // ASSERT
        Assert.True(creationResult.IsSuccess);
        Assert.NotEqual(Guid.Empty, creationResult.Value.Id);

        using IServiceScope assertionScope = _factory.Services.CreateScope();
        IBookingRepository bookingRepository = assertionScope.ServiceProvider.GetRequiredService<IBookingRepository>();

        DomainBooking? persistedBooking = await bookingRepository.GetByIdAsync(
            creationResult.Value.Id,
            cancellationToken);

        Assert.NotNull(persistedBooking);
        Assert.Equal(BookingStatus.PendingApproval, creationResult.Value.Status);
        Assert.NotNull(persistedBooking.PriceSnapshot);
        Assert.Equal(persistedBooking.PriceSnapshot.TotalPrice.Amount, creationResult.Value.TotalPrice);
        Assert.Equal(persistedBooking.PriceSnapshot.TotalPrice.Currency, creationResult.Value.Currency);
        Assert.Equal(data.PropertyId, persistedBooking.PropertyId);
        Assert.Equal(data.RentableUnitId, persistedBooking.RentableUnitId);
        Assert.Equal(new DateOnly(2026, 8, 10), persistedBooking.StayPeriod.CheckInDate);
        Assert.Equal(new DateOnly(2026, 8, 15), persistedBooking.StayPeriod.CheckOutDate);
        Assert.Equal(2, persistedBooking.GuestCount.Value);
        Assert.Equal(BookingStatus.PendingApproval, persistedBooking.Status);
        Assert.Null(persistedBooking.CancellationReason);
    }
}
