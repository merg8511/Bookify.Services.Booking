using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.IntegrationTests.Bookings;


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

        Property property =
            Property.Create(
                "Persistence Test Property",
                "America/El_Salvador",
                new TimeOnly(
                    15,
                    0),
                new TimeOnly(
                    11,
                    0))
            .Value;

        RentableUnit rentableUnit =
            RentableUnit.Create(
                property.Id,
                "Room A",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2)
            .Value;

        rentableUnit.ConfigurePricing(
    RentableUnitPricing.Create(
        Money.Create(
            100m,
            "USD")
        .Value,
        Money.Create(
            140m,
            "USD")
        .Value,
        Money.Create(
            25m,
            "USD")
        .Value)
    .Value);

        using (
            IServiceScope seedScope = _factory.Services.CreateScope())
        {
            IPropertyRepository propertyRepository =
                seedScope
                    .ServiceProvider
                        .GetRequiredService<IPropertyRepository>();

            IRentableUnitRepository rentableUnitRepository =
                seedScope
                    .ServiceProvider
                        .GetRequiredService<IRentableUnitRepository>();

            IUnitOfWork unitOfWork =
                seedScope
                    .ServiceProvider
                        .GetRequiredService<IUnitOfWork>();

            propertyRepository.Add(property);
            rentableUnitRepository.Add(rentableUnit);

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        Result<Guid> creationResult;

        using (
            IServiceScope commandScope =
                _factory.Services.CreateScope())
        {
            ICommandExecutor<
                CreateBookingCommand,
                Guid> executor =
                    commandScope
                        .ServiceProvider
                            .GetRequiredService<
                                ICommandExecutor<
                                    CreateBookingCommand,
                                    Guid>>();

            var command =
                new CreateBookingCommand(
                    property.Id,
                    rentableUnit.Id,
                    new DateOnly(
                        2026,
                        8,
                        10),
                    new DateOnly(
                        2026,
                        8,
                        15),
                    GuestCount: 2);

            // ACT
            creationResult = await executor.ExecuteAsync(command, cancellationToken);
        }

        // ASSERT
        Assert.True(creationResult.IsSuccess);

        Assert.NotEqual(
            Guid.Empty,
            creationResult.Value);

        using IServiceScope assertionScope =
            _factory.Services.CreateScope();

        IBookingRepository bookingRepository =
            assertionScope
                .ServiceProvider
                    .GetRequiredService<IBookingRepository>();

        DomainBooking? persistedBooking =
            await bookingRepository
                .GetByIdAsync(
                creationResult.Value,
                cancellationToken);

        Assert.NotNull(persistedBooking);

        Assert.Equal(
            property.Id,
            persistedBooking.PropertyId);

        Assert.Equal(
            rentableUnit.Id,
            persistedBooking.RentableUnitId);

        Assert.Equal(
            new DateOnly(
                2026,
                8,
                10),
            persistedBooking
                .StayPeriod
                .CheckInDate);

        Assert.Equal(
            new DateOnly(
                2026,
                8,
                15),
            persistedBooking
                .StayPeriod
                .CheckOutDate);

        Assert.Equal(
            2,
            persistedBooking
                .GuestCount
                .Value);

        Assert.Equal(
            BookingStatus.PendingApproval,
            persistedBooking.Status);

        Assert.Null(
            persistedBooking.CancellationReason);
    }
}
