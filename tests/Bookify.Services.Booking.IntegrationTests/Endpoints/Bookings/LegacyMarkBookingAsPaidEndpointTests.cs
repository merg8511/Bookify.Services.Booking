using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.IntegrationTests.Endpoints.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class LegacyMarkBookingAsPaidEndpointTests
{
    private readonly BookingApiFactory
        _factory;

    public LegacyMarkBookingAsPaidEndpointTests(
        BookingApiFactory factory)
    {
        _factory =
            factory;
    }

    [Fact]
    public async Task Post_WhenUsingLegacyMarkAsPaidRoute_ShouldReturnNotFoundAndKeepBookingPendingPayment()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            await SeedPendingPaymentBookingAsync(
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        // Act
        HttpResponseMessage response =
            await client.PostAsync(
                $"/api/v1/bookings/{bookingId:D}/mark-as-paid",
                content: null,
                cancellationToken);

        // Assert - HTTP
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        // Assert - persistent state was not changed
        using IServiceScope verificationScope =
            _factory.Services
                .CreateScope();

        IBookingRepository bookingRepository =
            verificationScope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        DomainBooking? persistedBooking =
            await bookingRepository
                .GetByIdAsync(
                    bookingId,
                    cancellationToken);

        Assert.NotNull(
            persistedBooking);

        Assert.Equal(
            BookingStatus.PendingPayment,
            persistedBooking.Status);

        Assert.Null(
            persistedBooking.CancellationReason);
    }

    private async Task<Guid>
        SeedPendingPaymentBookingAsync(
            CancellationToken cancellationToken)
    {
        Property property =
            Property.Create(
                $"Legacy MarkAsPaid {Guid.NewGuid():N}",
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
                $"Legacy Room {Guid.NewGuid():N}",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2)
            .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                new DateOnly(
                    2026,
                    10,
                    10),
                new DateOnly(
                    2026,
                    10,
                    12))
            .Value;

        DomainBooking booking =
            DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                GuestCount.Create(
                    2)
                .Value)
            .Value;

        Result approvalResult =
            booking.Approve();

        Assert.True(
            approvalResult.IsSuccess);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IPropertyRepository propertyRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IPropertyRepository>();

        IRentableUnitRepository rentableUnitRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IRentableUnitRepository>();

        IBookingRepository bookingRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        propertyRepository.Add(
            property);

        rentableUnitRepository.Add(
            rentableUnit);

        bookingRepository.Add(
            booking);

        await unitOfWork
            .SaveChangesAsync(
                cancellationToken);

        return booking.Id;
    }
}
