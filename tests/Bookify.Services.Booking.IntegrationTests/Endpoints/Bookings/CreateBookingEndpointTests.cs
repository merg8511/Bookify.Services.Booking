using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Bookings.Create;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Bookify.Services.Booking.Domain.Bookings;

namespace Bookify.Services.Booking.IntegrationTests.Endpoints.Bookings;

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
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        SeedData data = await SeedPropertyWithRoomAsync(cancellationToken);

        HttpClient client = _factory.CreateClient();

        var request =
            new CreateBookingRequest(
                data.PropertyId,
                data.RentableUnitId,
                Date(10),
                Date(15),
                GuestCount: 2);

        // ACT
        HttpResponseMessage response = await PostBookingAsync(request, cancellationToken);

        // ASSERT - HTTP
        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        Assert.Equal(
            "application/json",
            response.Content
                .Headers
                .ContentType?
                .MediaType);

        CreateBookingResponse? body =
            await response.Content
                .ReadFromJsonAsync<
                    CreateBookingResponse>(
                        cancellationToken);

        Assert.NotNull(body);

        Assert.NotEqual(
            Guid.Empty,
            body.Id);

        Assert.Equal(
            BookingStatus
                .PendingApproval
                .ToString(),
            body.Status);

        Assert.NotNull(response.Headers.Location);

        Assert.Equal(
            $"/api/v1/bookings/{body.Id}",
            response.Headers
                .Location
                .OriginalString);

        // ASSERT - PostgreSQL
        using IServiceScope scope = _factory.Services.CreateScope();

        IBookingRepository bookingRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        DomainBooking? booking =
            await bookingRepository
                .GetByIdAsync(
                    body.Id,
                    cancellationToken);

        Assert.NotNull(booking);

        Assert.Equal(
            data.PropertyId,
            booking.PropertyId);

        Assert.Equal(
            data.RentableUnitId,
            booking.RentableUnitId);

        Assert.Equal(
            Date(10),
            booking.StayPeriod
                .CheckInDate);

        Assert.Equal(
            Date(15),
            booking.StayPeriod
                .CheckOutDate);

        Assert.Equal(
            2,
            booking.GuestCount.Value);

        Assert.Equal(
            BookingStatus.PendingApproval,
            booking.Status);

        Assert.Equal(
            booking.Status.ToString(),
            body.Status);
    }

    [Fact]
    public async Task Post_WithInvalidGuestCount_ReturnsBadRequest()
    {
        // ARRANGE
        SeedData data = await SeedPropertyWithRoomAsync(TestContext.Current.CancellationToken);
        HttpClient client = _factory.CreateClient();

        var request =
            new CreateBookingRequest(
                data.PropertyId,
                data.RentableUnitId,
                Date(10),
                Date(15),
                GuestCount: 0);

        // ACT
        HttpResponseMessage response = await PostBookingAsync(request, TestContext.Current.CancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content
                .Headers
                .ContentType?
                .MediaType);
    }

    [Fact]
    public async Task Post_WithMissingProperty_ReturnsNotFound()
    {
        // ARRANGE
        HttpClient client = _factory.CreateClient();

        var request =
            new CreateBookingRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Date(10),
                Date(15),
                GuestCount: 2);

        // ACT
        HttpResponseMessage response = await PostBookingAsync(request, TestContext.Current.CancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content
                .Headers
                .ContentType?
                .MediaType);
    }

    [Fact]
    public async Task Post_WhenUnitIsAlreadyBooked_ReturnsConflict()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        SeedData data = await SeedPropertyWithRoomAsync(cancellationToken);

        HttpClient client = _factory.CreateClient();

        var request =
            new CreateBookingRequest(
                data.PropertyId,
                data.RentableUnitId,
                Date(10),
                Date(15),
                GuestCount: 2);

        HttpResponseMessage firstResponse = await PostBookingAsync(request, cancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        // ACT
        HttpResponseMessage secondResponse = await PostBookingAsync(request, cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.Conflict,
            secondResponse.StatusCode);

        Assert.Equal(
            "application/problem+json",
            secondResponse.Content
                .Headers
                .ContentType?
                .MediaType);
    }

    private async Task<HttpResponseMessage> PostBookingAsync(
        CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        HttpClient client = _factory.CreateClient();

        using var message =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v1/bookings");

        message.Headers
            .Add("Idempotency-Key",
                Guid.NewGuid()
                    .ToString("N"));

        message.Content = JsonContent.Create(request);

        return await client.SendAsync(
            message,
            cancellationToken);
    }
    private async Task<SeedData> SeedPropertyWithRoomAsync(CancellationToken cancellationToken)
    {
        Property property =
            Property.Create(
                    $"Booking API Test " +
                    $"{Guid.NewGuid():N}",
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

        using IServiceScope scope = _factory.Services.CreateScope();

        IPropertyRepository propertyRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IPropertyRepository>();

        IRentableUnitRepository
            rentableUnitRepository =
                scope.ServiceProvider
                    .GetRequiredService<
                        IRentableUnitRepository>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        propertyRepository.Add(property);
        rentableUnitRepository.Add(rentableUnit);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SeedData(property.Id, rentableUnit.Id);
    }

    private static DateOnly Date(int day)
    {
        return new DateOnly(
            2026,
            8,
            day);
    }

    private sealed record SeedData(
        Guid PropertyId,
        Guid RentableUnitId);
}
