using System.Net;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class LegacyMarkBookingAsPaidEndpointTests
{
    private readonly BookingApiFactory _factory;

    public LegacyMarkBookingAsPaidEndpointTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_WhenUsingLegacyMarkAsPaidRoute_ShouldReturnNotFoundAndKeepBookingPendingPayment()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory,
            BookingStatus.PendingPayment,
            cancellationToken: cancellationToken);

        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/bookings/{booking.Id:D}/mark-as-paid",
            content: null,
            cancellationToken);

        // Assert - HTTP
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Assert - persistent state was not changed
        using IServiceScope verificationScope = _factory.Services.CreateScope();
        IBookingRepository bookingRepository = verificationScope.ServiceProvider.GetRequiredService<IBookingRepository>();

        DomainBooking? persistedBooking = await bookingRepository.GetByIdAsync(booking.Id, cancellationToken);
        Assert.NotNull(persistedBooking);
        Assert.Equal(BookingStatus.PendingPayment, persistedBooking.Status);
        Assert.Null(persistedBooking.CancellationReason);
    }
}
