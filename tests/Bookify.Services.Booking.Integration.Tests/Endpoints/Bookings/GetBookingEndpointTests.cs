using Bookify.Services.Booking.Api.Endpoints.Bookings.Create;
using Bookify.Services.Booking.Api.Endpoints.Bookings.Get;
using Bookify.Services.Booking.Api.Endpoints.Payments.Initiate;
using Bookify.Services.Booking.Application.Abstractions.Security;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class GetBookingEndpointTests
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly BookingApiFactory _factory;

    public GetBookingEndpointTests(BookingApiFactory factory)
    {
        _factory = factory;
        _client = factory.Client;
    }

    [Fact]
    public async Task GetById_ShouldReturnCompleteBookingContract()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        CreatedBooking created =
            await CreateBookingAsync(cancellationToken);

        await ApproveBookingAsync(
            created.Response.Id,
            created.Seed.Property.OwnerSubjectId,
            cancellationToken);

        await InitiatePaymentAsync(
            created.Response.Id,
            created.GuestAccessToken,
            cancellationToken);

        using HttpClient guestClient =
            _factory.CreateGuestClient(created.GuestAccessToken);

        // Act
        using HttpResponseMessage response =
            await guestClient.GetAsync(
                created.Location,
                cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(
            "application/json",
            response.Content.Headers.ContentType?.MediaType);

        string responseJson =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        using JsonDocument document =
            JsonDocument.Parse(responseJson);

        Assert.True(
            document.RootElement.TryGetProperty(
                "rentableUnit",
                out _));

        Assert.False(
            document.RootElement.TryGetProperty(
                "rentableUnitResponse",
                out _));

        GetBookingResponse body =
            Assert.IsType<GetBookingResponse>(
                JsonSerializer.Deserialize<GetBookingResponse>(
                    responseJson,
                    SerializerOptions));

        Assert.Equal(
            created.Response.Id,
            body.Id);

        Assert.Equal(
            created.Response.BookingReference,
            body.BookingReference);

        Assert.Equal(
            created.Seed.PropertyId,
            body.PropertyId);

        Assert.Equal(
            created.Seed.Property.Name,
            body.PropertyName);

        Assert.Equal(
            created.Seed.RentableUnitId,
            body.RentableUnit.Id);

        Assert.Equal(
            created.Seed.RentableUnit.Name,
            body.RentableUnit.Name);

        Assert.Equal(
            Date(10),
            body.CheckInDate);

        Assert.Equal(
            Date(12),
            body.CheckOutDate);

        Assert.Equal(
            2,
            body.NumberOfNights);

        Assert.Equal(
            2,
            body.GuestCount);

        Assert.NotNull(body.Guest);

        Assert.Equal(
            "John Doe",
            body.Guest.FullName);

        Assert.Equal(
            "john@example.com",
            body.Guest.Email);

        Assert.Equal(
            "+50377778888",
            body.Guest.Phone);

        Assert.NotNull(body.Price);

        Assert.Equal(
            200m,
            body.Price.AccommodationPrice);

        Assert.Equal(
            0m,
            body.Price.ExtraGuestPrice);

        Assert.Equal(
            200m,
            body.Price.TotalPrice);

        Assert.Equal(
            "USD",
            body.Price.Currency);

        Assert.Equal(
            "PendingPayment",
            body.Status);

        Assert.Null(body.CancellationReason);

        Assert.Equal(
            "Pending",
            body.PaymentStatus);

        Assert.NotNull(body.CreatedAtUtc);
        Assert.NotNull(body.ApprovalDueAtUtc);
        Assert.NotNull(body.ApprovedAtUtc);
        Assert.NotNull(body.PaymentDueAtUtc);

        Assert.Equal(
            TimeSpan.FromHours(24),
            body.ApprovalDueAtUtc.Value -
            body.CreatedAtUtc.Value);

        Assert.Equal(
            TimeSpan.FromMinutes(30),
            body.PaymentDueAtUtc.Value -
            body.ApprovedAtUtc.Value);

        Assert.Null(body.PaidAtUtc);
        Assert.Null(body.CancelledAtUtc);
        Assert.Null(body.CompletedAtUtc);

        Assert.Equal(
            created.Response.CreatedAtUtc
                .ToUnixTimeMilliseconds(),
            body.CreatedAtUtc.Value
                .ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task GetByReference_ShouldReturnSameBooking()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        CreatedBooking created =
            await CreateBookingAsync(cancellationToken);

        string lowercaseReference =
            created.Response.BookingReference
                .ToLowerInvariant();

        string endpoint =
            $"/api/v1/bookings/{lowercaseReference}";

        using HttpClient guestClient =
            _factory.CreateGuestClient(
                created.GuestAccessToken);

        // Act
        using HttpResponseMessage response =
            await guestClient.GetAsync(
                endpoint,
                cancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        GetBookingResponse body =
            Assert.IsType<GetBookingResponse>(
                await response.Content
                    .ReadFromJsonAsync<GetBookingResponse>(
                        cancellationToken));

        Assert.Equal(
            created.Response.Id,
            body.Id);

        Assert.Equal(
            created.Response.BookingReference,
            body.BookingReference);

        Assert.Equal(
            "PendingApproval",
            body.Status);

        Assert.Null(body.PaymentStatus);
        Assert.NotNull(body.ApprovalDueAtUtc);
        Assert.Null(body.ApprovedAtUtc);
        Assert.Null(body.PaymentDueAtUtc);
    }

    [Fact]
    public async Task Get_WithInvalidIdentifier_ShouldReturnBadRequest()
    {
        // Arrange
        using HttpClient customerClient =
            _factory.CreateCustomerClient();

        // Act
        using HttpResponseMessage response =
            await customerClient.GetAsync(
                "/api/v1/bookings/not-a-booking",
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetailsResponse problem =
            Assert.IsType<ProblemDetailsResponse>(
                await response.Content
                    .ReadFromJsonAsync<ProblemDetailsResponse>(
                        TestContext.Current.CancellationToken));

        Assert.Equal(
            "Booking.InvalidIdentifier",
            problem.Code);
    }

    [Fact]
    public async Task Get_WithUnknownReference_ShouldReturnNotFound()
    {
        // Arrange
        BookingReference reference =
            BookingReference.New();

        string endpoint =
            $"/api/v1/bookings/{reference.Value}";

        using HttpClient customerClient =
            _factory.CreateCustomerClient();

        // Act
        using HttpResponseMessage response =
            await customerClient.GetAsync(
                endpoint,
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        ProblemDetailsResponse problem =
            Assert.IsType<ProblemDetailsResponse>(
                await response.Content
                    .ReadFromJsonAsync<ProblemDetailsResponse>(
                        TestContext.Current.CancellationToken));

        Assert.Equal(
            "Booking.NotFound",
            problem.Code);
    }

    private async Task<CreatedBooking> CreateBookingAsync(
        CancellationToken cancellationToken)
    {
        SeedData seed =
            await BookingDatabaseTestSeeder
                .SeedPropertyWithRoomAsync(
                    _factory,
                    propertyName:
                        $"Get Booking Test {Guid.NewGuid():N}",
                    weekdayPrice: 100m,
                    weekendPrice: 100m,
                    cancellationToken:
                        cancellationToken);

        CreateBookingRequest request =
            BookingRequestTestFactory
                .CreateBookingRequest(
                    seed.PropertyId,
                    seed.RentableUnitId,
                    Date(10),
                    Date(12),
                    guestCount: 2);

        using var message =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v1/bookings");

        message.Headers.Add(
            "Idempotency-Key",
            $"booking-get-test-{Guid.NewGuid():N}");

        message.Content =
            JsonContent.Create(request);

        using HttpResponseMessage response =
            await _client.SendAsync(
                message,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        string guestAccessToken =
            Assert.Single(
                response.Headers.GetValues(
                    "Booking-Guest-Token"));

        CreateBookingResponse body =
            Assert.IsType<CreateBookingResponse>(
                await response.Content
                    .ReadFromJsonAsync<CreateBookingResponse>(
                        cancellationToken));

        Uri location =
            Assert.IsType<Uri>(
                response.Headers.Location);

        Assert.EndsWith(
            $"/api/v1/bookings/{body.Id}",
            location.ToString(),
            StringComparison.Ordinal);

        return new CreatedBooking(
            seed,
            body,
            location,
            guestAccessToken);
    }

    private async Task ApproveBookingAsync(
        Guid bookingId,
        string ownerSubjectId,
        CancellationToken cancellationToken)
    {
        using HttpClient ownerClient =
            _factory.CreateAuthenticatedClient(
                ownerSubjectId,
                BookifyRoles.Owner);

        using HttpResponseMessage response =
            await ownerClient.PostAsync(
                $"/api/v1/bookings/{bookingId}/approve",
                content: null,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);
    }

    private async Task InitiatePaymentAsync(
        Guid bookingId,
        string guestAccessToken,
        CancellationToken cancellationToken)
    {
        var request =
            new InitiatePaymentRequest(
                bookingId);

        using HttpClient guestClient =
            _factory.CreateGuestClient(
                guestAccessToken);

        using var message =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v1/payments");

        message.Headers.Add(
            "Idempotency-Key",
            $"booking-get-payment-{Guid.NewGuid():N}");

        message.Content =
            JsonContent.Create(request);

        using HttpResponseMessage response =
            await guestClient.SendAsync(
                message,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    private static DateOnly Date(int day)
    {
        return BookingTestData.CreateDate(
            day,
            month: 11,
            year: 2026);
    }

    private sealed record CreatedBooking(
        SeedData Seed,
        CreateBookingResponse Response,
        Uri Location,
        string GuestAccessToken);
}
