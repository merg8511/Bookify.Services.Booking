using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Bookings.Create;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class CreateBookingEndpointTests
{
    private readonly BookingApiFactory _factory;

    public CreateBookingEndpointTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_WithValidRequest_ReturnsCreatedAndPersistsBooking()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeedData data = await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(_factory, cancellationToken: cancellationToken);

        var request = BookingRequestTestFactory.CreateBookingRequest(
            data.PropertyId,
            data.RentableUnitId,
            Date(10),
            Date(15),
            guestCount: 2);

        // Act
        HttpResponseMessage response = await PostBookingAsync(request, cancellationToken);

        // Assert - HTTP
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        CreateBookingResponse? body = await response.Content.ReadFromJsonAsync<CreateBookingResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal(BookingStatus.PendingApproval.ToString(), body.Status);
        Assert.NotNull(body.Price);
        Assert.Equal(540m, body.Price.AccommodationPrice);
        Assert.Equal(0m, body.Price.ExtraGuestPrice);
        Assert.Equal(540m, body.Price.TotalPrice);
        Assert.Equal("USD", body.Price.Currency);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal($"/api/v1/bookings/{body.Id}", response.Headers.Location.OriginalString);

        // Assert - PostgreSQL
        using IServiceScope scope = _factory.Services.CreateScope();
        IBookingRepository bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();

        DomainBooking? booking = await bookingRepository.GetByIdAsync(body.Id, cancellationToken);
        Assert.NotNull(booking);
        Assert.Equal(data.PropertyId, booking.PropertyId);
        Assert.Equal(data.RentableUnitId, booking.RentableUnitId);
        Assert.Equal(Date(10), booking.StayPeriod.CheckInDate);
        Assert.Equal(Date(15), booking.StayPeriod.CheckOutDate);
        Assert.Equal(2, booking.GuestCount.Value);
        Assert.Equal(BookingStatus.PendingApproval, booking.Status);
        Assert.Equal(booking.Status.ToString(), body.Status);
        Assert.NotNull(booking.PriceSnapshot);
        Assert.Equal(booking.PriceSnapshot.AccommodationPrice.Amount, body.Price.AccommodationPrice);
        Assert.Equal(booking.PriceSnapshot.ExtraGuestPrice.Amount, body.Price.ExtraGuestPrice);
        Assert.Equal(booking.PriceSnapshot.TotalPrice.Amount, body.Price.TotalPrice);
        Assert.Equal(booking.PriceSnapshot.TotalPrice.Currency, body.Price.Currency);
    }

    [Fact]
    public async Task Post_WithInvalidGuestCount_ReturnsBadRequest()
    {
        // Arrange
        SeedData data = await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(_factory, cancellationToken: TestContext.Current.CancellationToken);

        var request = BookingRequestTestFactory.CreateBookingRequest(
            data.PropertyId,
            data.RentableUnitId,
            Date(10),
            Date(15),
            guestCount: 0);

        // Act
        HttpResponseMessage response = await PostBookingAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_WithMissingProperty_ReturnsNotFound()
    {
        // Arrange
        var request = BookingRequestTestFactory.CreateBookingRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Date(10),
            Date(15),
            guestCount: 2);

        // Act
        HttpResponseMessage response = await PostBookingAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_WhenUnitIsAlreadyBooked_ReturnsConflict()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeedData data = await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(_factory, cancellationToken: cancellationToken);

        var request = BookingRequestTestFactory.CreateBookingRequest(
            data.PropertyId,
            data.RentableUnitId,
            Date(10),
            Date(15),
            guestCount: 2);

        HttpResponseMessage firstResponse = await PostBookingAsync(request, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Act
        HttpResponseMessage secondResponse = await PostBookingAsync(request, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal("application/problem+json", secondResponse.Content.Headers.ContentType?.MediaType);
    }

    private async Task<HttpResponseMessage> PostBookingAsync(
        CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        HttpClient client = _factory.CreateClient();

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bookings");
        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        message.Content = JsonContent.Create(request);

        return await client.SendAsync(message, cancellationToken);
    }

    private static DateOnly Date(int day) => BookingTestData.CreateDate(day);
}
