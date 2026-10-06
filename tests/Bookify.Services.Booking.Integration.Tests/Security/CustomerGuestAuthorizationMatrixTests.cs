using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Payments.Initiate;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Security;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Security")]
public sealed class CustomerGuestAuthorizationMatrixTests
{
    private const string GetBooking =
        "get-booking";

    private const string CancelBooking =
        "cancel-booking";

    private const string InitiatePayment =
        "initiate-payment";

    private const string GetPaymentStatus =
        "get-payment-status";

    private readonly BookingApiFactory _factory;

    public CustomerGuestAuthorizationMatrixTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<
        string,
        string,
        HttpStatusCode> AccessCases
    { get; } =
        CreateAccessCases();

    [Theory]
    [MemberData(nameof(AccessCases))]
    public async Task BookingAccess_ShouldEnforceAuthorizationMatrix(
        string endpoint,
        string actor,
        HttpStatusCode expectedStatusCode)
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        AuthorizationBooking seed =
            await SeedBookingAsync(
                endpoint,
                actor,
                cancellationToken);

        using HttpClient client =
            CreateClient(
                actor,
                seed.GuestAccessToken);

        // Act
        using HttpResponseMessage response =
            await SendAsync(
                endpoint,
                client,
                seed.Booking,
                cancellationToken);

        // Assert
        Assert.Equal(
            expectedStatusCode,
            response.StatusCode);
    }

    [Fact]
    public async Task GetBooking_ByReference_WithValidGuestCredential_ShouldReturnOk()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        GuestBookingSeedData guest =
            await BookingDatabaseTestSeeder
                .SeedGuestBookingAsync(
                    _factory,
                    status:
                        BookingStatus.PendingApproval,
                    cancellationToken:
                        cancellationToken);

        using HttpClient client =
            _factory.CreateGuestClient(
                guest.GuestAccessToken);

        string reference =
            Uri.EscapeDataString(
                guest.Booking.Reference.Value);

        // Act
        using HttpResponseMessage response =
            await client.GetAsync(
                $"/api/v1/bookings/{reference}",
                cancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task GetBooking_ByReference_WithDifferentCustomer_ShouldReturnForbidden()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await BookingDatabaseTestSeeder
                .SeedBookingAsync(
                    _factory,
                    status:
                        BookingStatus.PendingApproval,
                    customerSubjectId:
                        TestIdentitySubjects.Customer,
                    cancellationToken:
                        cancellationToken);

        using HttpClient client =
            _factory.CreateCustomerClient(
                $"different-customer-{Guid.NewGuid():N}");

        string reference =
            Uri.EscapeDataString(
                booking.Reference.Value);

        // Act
        using HttpResponseMessage response =
            await client.GetAsync(
                $"/api/v1/bookings/{reference}",
                cancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    private static TheoryData<
        string,
        string,
        HttpStatusCode> CreateAccessCases()
    {
        var cases =
            new TheoryData<
                string,
                string,
                HttpStatusCode>();

        string[] endpoints =
        [
            GetBooking,
            CancelBooking,
            InitiatePayment,
            GetPaymentStatus
        ];

        foreach (string endpoint in endpoints)
        {
            HttpStatusCode success =
                GetSuccessStatusCode(
                    endpoint);

            cases.Add(
                endpoint,
                "anonymous",
                HttpStatusCode.Unauthorized);

            cases.Add(
                endpoint,
                "wrong-customer",
                HttpStatusCode.Forbidden);

            cases.Add(
                endpoint,
                "correct-customer",
                success);

            cases.Add(
                endpoint,
                "owner",
                HttpStatusCode.Forbidden);

            cases.Add(
                endpoint,
                "admin",
                HttpStatusCode.Forbidden);

            cases.Add(
                endpoint,
                "invalid-guest",
                HttpStatusCode.Unauthorized);

            cases.Add(
                endpoint,
                "valid-guest",
                success);
        }

        return cases;
    }

    private async Task<AuthorizationBooking>
        SeedBookingAsync(
            string endpoint,
            string actor,
            CancellationToken cancellationToken)
    {
        BookingStatus requiredStatus =
            endpoint is InitiatePayment or GetPaymentStatus
                ? BookingStatus.PendingPayment
                : BookingStatus.PendingApproval;

        if (actor is
            "valid-guest" or
            "invalid-guest")
        {
            GuestBookingSeedData guest =
                await BookingDatabaseTestSeeder
                    .SeedGuestBookingAsync(
                        _factory,
                        status:
                            requiredStatus,
                        cancellationToken:
                            cancellationToken);

            return new AuthorizationBooking(
                guest.Booking,
                guest.GuestAccessToken);
        }

        DomainBooking booking =
            await BookingDatabaseTestSeeder
                .SeedBookingAsync(
                    _factory,
                    status:
                        requiredStatus,
                    ownerSubjectId:
                        TestIdentitySubjects.Owner,
                    customerSubjectId:
                        TestIdentitySubjects.Customer,
                    cancellationToken:
                        cancellationToken);

        return new AuthorizationBooking(
            booking,
            GuestAccessToken: null);
    }

    private HttpClient CreateClient(
        string actor,
        string? guestAccessToken)
    {
        return actor switch
        {
            "anonymous" =>
                _factory.CreateClient(),

            "wrong-customer" =>
                _factory.CreateCustomerClient(
                    $"different-customer-{Guid.NewGuid():N}"),

            "correct-customer" =>
                _factory.CreateCustomerClient(
                    TestIdentitySubjects.Customer),

            "owner" =>
                _factory.CreateOwnerClient(),

            "admin" =>
                _factory.CreateAdminClient(),

            "invalid-guest" =>
                _factory.CreateGuestClient(
                    new string('x', 43)),

            "valid-guest" =>
                _factory.CreateGuestClient(
                    guestAccessToken ??
                    throw new InvalidOperationException(
                        "A valid guest actor requires " +
                        "a guest access token.")),

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(actor),
                    actor,
                    "Unsupported authorization actor.")
        };
    }

    private static async Task<HttpResponseMessage>
        SendAsync(
            string endpoint,
            HttpClient client,
            DomainBooking booking,
            CancellationToken cancellationToken)
    {
        return endpoint switch
        {
            GetBooking =>
                await client.GetAsync(
                    $"/api/v1/bookings/{booking.Id:D}",
                    cancellationToken),

            CancelBooking =>
                await client.PostAsync(
                    $"/api/v1/bookings/" +
                    $"{booking.Id:D}/cancel",
                    content: null,
                    cancellationToken),

            InitiatePayment =>
                await SendInitiatePaymentAsync(
                    client,
                    booking.Id,
                    cancellationToken),

            GetPaymentStatus =>
                await client.GetAsync(
                    $"/api/v1/payments/bookings/" +
                    $"{booking.Id:D}/status",
                    cancellationToken),

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(endpoint),
                    endpoint,
                    "Unsupported protected endpoint.")
        };
    }

    private static async Task<HttpResponseMessage>
        SendInitiatePaymentAsync(
            HttpClient client,
            Guid bookingId,
            CancellationToken cancellationToken)
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v1/payments");

        request.Headers.Add(
            "Idempotency-Key",
            $"authorization-{Guid.NewGuid():N}");

        request.Content =
            JsonContent.Create(
                new InitiatePaymentRequest(
                    bookingId));

        return await client.SendAsync(
            request,
            cancellationToken);
    }

    private static HttpStatusCode
        GetSuccessStatusCode(
            string endpoint)
    {
        return endpoint == CancelBooking
            ? HttpStatusCode.NoContent
            : HttpStatusCode.OK;
    }

    private sealed record AuthorizationBooking(
        DomainBooking Booking,
        string? GuestAccessToken);
}
