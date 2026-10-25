
using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Administrative.Bookings;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class AdminBookingActionsEndpointTests
{
    private readonly BookingApiFactory _factory;

    private static CancellationToken CancellationToken =>
        TestContext.Current.CancellationToken;

    public AdminBookingActionsEndpointTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    private static string BookingUrl(
        Guid propertyId,
        Guid bookingId) =>
        $"/api/v1/admin/properties/{propertyId}/bookings/{bookingId}";

    private static string ActionUrl(
        Guid propertyId,
        Guid bookingId,
        string action) =>
        $"{BookingUrl(propertyId, bookingId)}/{action}";

    [Fact]
    public async Task Approve_AsOwner_ShouldPersistPendingPayment()
    {
        // ARRANGE
        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingApproval,
                cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        string actionUrl = ActionUrl(
            booking.PropertyId,
            booking.Id,
            "approve");

        // ACT
        using HttpResponseMessage response =
            await owner.PostAsync(
                actionUrl,
                content: null,
                CancellationToken);

        // ASSERT — Existing command is executed successfully.
        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);

        // Verify through the administrative read side of 21.5.
        using HttpResponseMessage detailResponse =
            await owner.GetAsync(
                BookingUrl(booking.PropertyId, booking.Id),
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            detailResponse.StatusCode);

        AdminBookingDetailsResponse persisted =
            Assert.IsType<AdminBookingDetailsResponse>(
                await detailResponse.Content
                    .ReadFromJsonAsync<AdminBookingDetailsResponse>(
                        CancellationToken));

        Assert.Equal(booking.Id, persisted.Id);
        Assert.Equal("PendingPayment", persisted.Status);
        Assert.NotNull(persisted.ApprovedAtUtc);
        Assert.NotNull(persisted.PaymentDueAtUtc);

        // The existing Domain transition still rejects approval twice.
        using HttpResponseMessage repeatedResponse =
            await owner.PostAsync(
                actionUrl,
                content: null,
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.Conflict,
            repeatedResponse.StatusCode);

        ProblemDetailsResponse problem =
            Assert.IsType<ProblemDetailsResponse>(
                await repeatedResponse.Content
                    .ReadFromJsonAsync<ProblemDetailsResponse>(
                        CancellationToken));

        Assert.Equal(
            "Booking.InvalidStatusTransition",
            problem.Code);
    }

    [Fact]
    public async Task Reject_AsOwner_ShouldPreserveRejectionReason()
    {
        // ARRANGE
        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingApproval,
                cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        // ACT
        using HttpResponseMessage response =
            await owner.PostAsync(
                ActionUrl(
                    booking.PropertyId,
                    booking.Id,
                    "reject"),
                content: null,
                CancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.NoContent,
            response.StatusCode);

        using HttpResponseMessage detailResponse =
            await owner.GetAsync(
                BookingUrl(booking.PropertyId, booking.Id),
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            detailResponse.StatusCode);

        AdminBookingDetailsResponse persisted =
            Assert.IsType<AdminBookingDetailsResponse>(
                await detailResponse.Content
                    .ReadFromJsonAsync<AdminBookingDetailsResponse>(
                        CancellationToken));

        Assert.Equal(booking.Id, persisted.Id);
        Assert.Equal("Cancelled", persisted.Status);
        Assert.Equal(
            "RejectedByOwner",
            persisted.CancellationReason);
        Assert.NotNull(persisted.CancelledAtUtc);
    }

    [Fact]
    public async Task Action_WithBookingFromDifferentProperty_ShouldReturnNotFound()
    {
        // ARRANGE
        DomainBooking bookingA =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingApproval,
                cancellationToken: CancellationToken);

        DomainBooking bookingB =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingApproval,
                cancellationToken: CancellationToken);

        // Both properties may belong to the same owner.
        // The URL must still respect the Booking/Property relationship.
        using HttpClient owner = _factory.CreateOwnerClient();
        using HttpClient admin = _factory.CreateAdminClient();

        string invalidApproveUrl = ActionUrl(
            bookingA.PropertyId,
            bookingB.Id,
            "approve");

        string invalidRejectUrl = ActionUrl(
            bookingA.PropertyId,
            bookingB.Id,
            "reject");

        // ACT
        using HttpResponseMessage ownerResponse =
            await owner.PostAsync(
                invalidApproveUrl,
                content: null,
                CancellationToken);

        using HttpResponseMessage adminResponse =
            await admin.PostAsync(
                invalidRejectUrl,
                content: null,
                CancellationToken);

        // ASSERT — Neither caller may act through a mismatched URL.
        Assert.Equal(
            HttpStatusCode.NotFound,
            ownerResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            adminResponse.StatusCode);

        // Verify that Booking B was not modified.
        using HttpResponseMessage detailResponse =
            await admin.GetAsync(
                BookingUrl(
                    bookingB.PropertyId,
                    bookingB.Id),
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            detailResponse.StatusCode);

        AdminBookingDetailsResponse persisted =
            Assert.IsType<AdminBookingDetailsResponse>(
                await detailResponse.Content
                    .ReadFromJsonAsync<AdminBookingDetailsResponse>(
                        CancellationToken));

        Assert.Equal(
            "PendingApproval",
            persisted.Status);

        Assert.Null(persisted.CancelledAtUtc);
        Assert.Null(persisted.ApprovedAtUtc);
    }

    [Fact]
    public async Task Approve_ShouldEnforceAdministrativeAuthorization()
    {
        // ARRANGE
        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingApproval,
                cancellationToken: CancellationToken);

        string endpoint = ActionUrl(
            booking.PropertyId,
            booking.Id,
            "approve");

        using HttpClient wrongOwner =
            _factory.CreateOwnerClient("different-owner");

        using HttpClient customer =
            _factory.CreateCustomerClient();

        using HttpClient admin =
            _factory.CreateAdminClient();

        // ACT — Anonymous
        using HttpResponseMessage anonymousResponse =
            await _factory.Client.PostAsync(
                endpoint,
                content: null,
                CancellationToken);

        // ACT — Owner of another property
        using HttpResponseMessage wrongOwnerResponse =
            await wrongOwner.PostAsync(
                endpoint,
                content: null,
                CancellationToken);

        // ACT — Customer
        using HttpResponseMessage customerResponse =
            await customer.PostAsync(
                endpoint,
                content: null,
                CancellationToken);

        // ACT — Admin
        using HttpResponseMessage adminResponse =
            await admin.PostAsync(
                endpoint,
                content: null,
                CancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            anonymousResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            wrongOwnerResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            customerResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.NoContent,
            adminResponse.StatusCode);
    }

    [Fact]
    public async Task CustomerCancellation_ShouldKeepOriginalAuthorization()
    {
        // ARRANGE
        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingApproval,
                customerSubjectId: TestIdentitySubjects.Customer,
                cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();
        using HttpClient customer = _factory.CreateCustomerClient();
        using HttpClient admin = _factory.CreateAdminClient();

        string cancelUrl =
            $"/api/v1/bookings/{booking.Id}/cancel";

        // ACT — Owner must not acquire customer cancellation access.
        using HttpResponseMessage ownerResponse =
            await owner.PostAsync(
                cancelUrl,
                content: null,
                CancellationToken);

        // ACT — Booking customer may cancel.
        using HttpResponseMessage customerResponse =
            await customer.PostAsync(
                cancelUrl,
                content: null,
                CancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.Forbidden,
            ownerResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.NoContent,
            customerResponse.StatusCode);

        // The cancellation reason must remain CancelledByGuest.
        using HttpResponseMessage detailResponse =
            await admin.GetAsync(
                BookingUrl(
                    booking.PropertyId,
                    booking.Id),
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            detailResponse.StatusCode);

        AdminBookingDetailsResponse persisted =
            Assert.IsType<AdminBookingDetailsResponse>(
                await detailResponse.Content
                    .ReadFromJsonAsync<AdminBookingDetailsResponse>(
                        CancellationToken));

        Assert.Equal("Cancelled", persisted.Status);

        Assert.Equal(
            BookingCancellationReason.CancelledByGuest.ToString(),
            persisted.CancellationReason);
    }
}
