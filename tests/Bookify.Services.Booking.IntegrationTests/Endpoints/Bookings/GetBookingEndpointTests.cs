using Bookify.Services.Booking.Api.Endpoints.Bookings.Create;
using Bookify.Services.Booking.Api.Endpoints.Bookings.Get;
using Bookify.Services.Booking.Api.Endpoints.Payments.Initiate;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.IntegrationTests.Contracts;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Bookify.Services.Booking.IntegrationTests.Endpoints.Bookings;

[Collection(
    BookingApiTestFixture.Name)]
[Trait(
    "Category",
    "Integration")]
public sealed class GetBookingEndpointTests
{
    private readonly HttpClient _client;
    private readonly BookingApiFactory _factory;

    public GetBookingEndpointTests(
        BookingApiFactory factory)
    {
        _factory =
            factory;

        _client =
            factory.Client;
    }

    [Fact]
    public async Task
        GetById_ShouldReturnCompleteBookingContract()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        CreatedBooking created =
            await CreateBookingAsync(
                cancellationToken);

        await ApproveBookingAsync(
            created.Response.Id,
            cancellationToken);

        await InitiatePaymentAsync(
            created.Response.Id,
            cancellationToken);

        // ACT
        using HttpResponseMessage response =
            await _client.GetAsync(
                created.Location,
                cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.Equal(
            "application/json",
            response.Content.Headers
                .ContentType?
                .MediaType);

        GetBookingResponse body =
            Assert.IsType<
                GetBookingResponse>(
                    await response.Content
                        .ReadFromJsonAsync<
                            GetBookingResponse>(
                                cancellationToken));

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
            created.Seed.PropertyName,
            body.PropertyName);

        Assert.Equal(
            created.Seed.RentableUnitId,
            body.RentableUnitResponse.Id);

        Assert.Equal(
            created.Seed.RentableUnitName,
            body.RentableUnitResponse.Name);

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

        Assert.NotNull(
            body.Guest);

        Assert.Equal(
            "John Doe",
            body.Guest.FullName);

        Assert.Equal(
            "john@example.com",
            body.Guest.Email);

        Assert.Equal(
            "+50377778888",
            body.Guest.Phone);

        Assert.NotNull(
            body.Price);

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

        Assert.Null(
            body.CancellationReason);

        Assert.Equal(
            "Pending",
            body.PaymentStatus);

        Assert.NotNull(
            body.CreatedAtUtc);

        Assert.NotNull(
            body.ApprovalDueAtUtc);

        Assert.NotNull(
            body.ApprovedAtUtc);

        Assert.NotNull(
            body.PaymentDueAtUtc);

        Assert.Equal(
            TimeSpan.FromHours(24),
            body.ApprovalDueAtUtc.Value -
            body.CreatedAtUtc.Value);

        Assert.Equal(
            TimeSpan.FromMinutes(30),
            body.PaymentDueAtUtc.Value -
            body.ApprovedAtUtc.Value);

        Assert.Null(
            body.PaidAtUtc);

        Assert.Null(
            body.CancelledAtUtc);

        Assert.Null(
            body.CompletedAtUtc);

        Assert.Equal(
            created.Response
                .CreatedAtUtc
                .ToUnixTimeMilliseconds(),
            body.CreatedAtUtc.Value
                .ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task
        GetByReference_ShouldReturnSameBooking()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        CreatedBooking created =
            await CreateBookingAsync(
                cancellationToken);

        string lowercaseReference =
            created.Response
                .BookingReference
                .ToLowerInvariant();

        string endpoint =
            $"/api/v1/bookings/" +
            $"{lowercaseReference}";

        // ACT
        using HttpResponseMessage response =
            await _client.GetAsync(
                endpoint,
                cancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        GetBookingResponse body =
            Assert.IsType<
                GetBookingResponse>(
                    await response.Content
                        .ReadFromJsonAsync<
                            GetBookingResponse>(
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

        Assert.Null(
            body.PaymentStatus);

        Assert.NotNull(
            body.ApprovalDueAtUtc);

        Assert.Null(
            body.ApprovedAtUtc);

        Assert.Null(
            body.PaymentDueAtUtc);
    }

    [Fact]
    public async Task
        Get_WithInvalidIdentifier_ShouldReturnBadRequest()
    {
        // ACT
        using HttpResponseMessage response =
            await _client.GetAsync(
                "/api/v1/bookings/not-a-booking",
                TestContext.Current
                    .CancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetailsResponse problem =
            Assert.IsType<
                ProblemDetailsResponse>(
                    await response.Content
                        .ReadFromJsonAsync<
                            ProblemDetailsResponse>(
                                TestContext.Current
                                    .CancellationToken));

        Assert.Equal(
            "Booking.InvalidIdentifier",
            problem.Code);
    }

    [Fact]
    public async Task
        Get_WithUnknownReference_ShouldReturnNotFound()
    {
        // ARRANGE
        BookingReference reference =
            BookingReference.New();

        string endpoint =
            $"/api/v1/bookings/" +
            $"{reference.Value}";

        // ACT
        using HttpResponseMessage response =
            await _client.GetAsync(
                endpoint,
                TestContext.Current
                    .CancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        ProblemDetailsResponse problem =
            Assert.IsType<
                ProblemDetailsResponse>(
                    await response.Content
                        .ReadFromJsonAsync<
                            ProblemDetailsResponse>(
                                TestContext.Current
                                    .CancellationToken));

        Assert.Equal(
            "Booking.NotFound",
            problem.Code);
    }

    private async Task<CreatedBooking>
        CreateBookingAsync(
            CancellationToken cancellationToken)
    {
        SeedData seed =
            await SeedPropertyWithRoomAsync(
                cancellationToken);

        var request =
            new CreateBookingRequest(
                seed.PropertyId,
                seed.RentableUnitId,
                Date(10),
                Date(12),
                GuestCount: 2,
                Guest:
                    new CreateBookingGuestRequest(
                        "John Doe",
                        "john@example.com",
                        "+50377778888"));

        using var message =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v1/bookings");

        message.Headers.Add(
            "Idempotency-Key",
            $"booking-get-test-" +
            $"{Guid.NewGuid():N}");

        message.Content =
            JsonContent.Create(
                request);

        using HttpResponseMessage response =
            await _client.SendAsync(
                message,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        CreateBookingResponse body =
            Assert.IsType<
                CreateBookingResponse>(
                    await response.Content
                        .ReadFromJsonAsync<
                            CreateBookingResponse>(
                                cancellationToken));

        Uri location =
            Assert.IsType<Uri>(
                response.Headers.Location);

        Assert.EndsWith(
            $"/api/v1/bookings/" +
            $"{body.Id}",
            location.ToString(),
            StringComparison.Ordinal);

        return new CreatedBooking(
            seed,
            body,
            location);
    }

    private async Task ApproveBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _client.PostAsync(
                $"/api/v1/bookings/" +
                $"{bookingId}/approve",
                content: null,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);
    }

    private async Task InitiatePaymentAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var request =
            new InitiatePaymentRequest(
                bookingId);

        using var message =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v1/payments");

        message.Headers.Add(
            "Idempotency-Key",
            $"booking-get-payment-" +
            $"{Guid.NewGuid():N}");

        message.Content =
            JsonContent.Create(
                request);

        using HttpResponseMessage response =
            await _client.SendAsync(
                message,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    private async Task<SeedData>
        SeedPropertyWithRoomAsync(
            CancellationToken cancellationToken)
    {
        string propertyName =
            $"Get Booking Test " +
            $"{Guid.NewGuid():N}";

        string rentableUnitName =
            $"Room " +
            $"{Guid.NewGuid():N}";

        Property property =
            Property.Create(
                    propertyName,
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
                    rentableUnitName,
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
                            100m,
                            "USD")
                        .Value,
                    Money.Create(
                            25m,
                            "USD")
                        .Value)
                .Value);

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

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        propertyRepository.Add(
            property);

        rentableUnitRepository.Add(
            rentableUnit);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return new SeedData(
            property.Id,
            propertyName,
            rentableUnit.Id,
            rentableUnitName);
    }

    private static DateOnly Date(
        int day)
    {
        return new DateOnly(
            2026,
            11,
            day);
    }

    private sealed record SeedData(
        Guid PropertyId,
        string PropertyName,
        Guid RentableUnitId,
        string RentableUnitName);

    private sealed record CreatedBooking(
        SeedData Seed,
        CreateBookingResponse Response,
        Uri Location);
}
