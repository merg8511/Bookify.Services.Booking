
using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Contracts.Pagination;
using Bookify.Services.Booking.Api.Endpoints.Administrative.Bookings;
using Bookify.Services.Booking.Api.Endpoints.Administrative.Bookings.Calendar;
using Bookify.Services.Booking.Api.Endpoints.Administrative.Bookings.Payment;
using Bookify.Services.Booking.Api.Endpoints.Bookings.Get;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class AdminBookingWorkflowAndConcurrencyTests
{
    private readonly BookingApiFactory _factory;

    private static CancellationToken CancellationToken =>
        TestContext.Current.CancellationToken;

    public AdminBookingWorkflowAndConcurrencyTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Reject_ShouldRemainConsistentAcrossBookingCalendarAndPaymentReads()
    {
        // ARRANGE
        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingApproval,
                customerSubjectId: TestIdentitySubjects.Customer,
                cancellationToken: CancellationToken);

        using HttpClient owner =
            _factory.CreateOwnerClient();

        using HttpClient customer =
            _factory.CreateCustomerClient();

        string propertyUrl =
            $"/api/v1/admin/properties/{booking.PropertyId}";

        string bookingsUrl =
            $"{propertyUrl}/bookings";

        string bookingUrl =
            $"{bookingsUrl}/{booking.Id}";

        string calendarUrl =
            $"{propertyUrl}/calendar" +
            "?rangeStart=2026-09-10&rangeEnd=2026-09-12";

        // BEFORE — Booking is PendingApproval.
        PagedResponse<AdminBookingListItemResponse> pending =
            await GetJsonAsync<
                PagedResponse<AdminBookingListItemResponse>>(
                    owner,
                    $"{bookingsUrl}?status=PendingApproval");

        Assert.Contains(
            pending.Items,
            item => item.Id == booking.Id);

        // BEFORE — Booking blocks inventory.
        GetAdminBookingCalendarResponse beforeCalendar =
            await GetJsonAsync<GetAdminBookingCalendarResponse>(
                owner,
                $"{calendarUrl}&blocksInventory=true");

        AdminBookingCalendarItemResponse beforeItem =
            Assert.Single(beforeCalendar.Items);

        Assert.Equal(booking.Id, beforeItem.BookingId);
        Assert.True(beforeItem.BlocksInventory);

        // ACT — Owner rejects the reservation through 21.7.
        using HttpResponseMessage rejectResponse =
            await owner.PostAsync(
                $"{bookingUrl}/reject",
                content: null,
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.NoContent,
            rejectResponse.StatusCode);

        // AFTER — Booking list reflects the cancellation.
        PagedResponse<AdminBookingListItemResponse> cancelled =
            await GetJsonAsync<
                PagedResponse<AdminBookingListItemResponse>>(
                    owner,
                    $"{bookingsUrl}?status=Cancelled");

        Assert.Contains(
            cancelled.Items,
            item => item.Id == booking.Id);

        // AFTER — Calendar no longer considers it blocking.
        GetAdminBookingCalendarResponse afterCalendar =
            await GetJsonAsync<GetAdminBookingCalendarResponse>(
                owner,
                $"{calendarUrl}&blocksInventory=false");

        AdminBookingCalendarItemResponse afterItem =
            Assert.Single(afterCalendar.Items);

        Assert.Equal(booking.Id, afterItem.BookingId);
        Assert.Equal("Cancelled", afterItem.Status);
        Assert.False(afterItem.BlocksInventory);

        // AFTER — Payment administration remains coherent.
        // Rejection did not initiate a payment.
        GetAdminBookingPaymentResponse payment =
            await GetJsonAsync<GetAdminBookingPaymentResponse>(
                owner,
                $"{bookingUrl}/payment");

        Assert.Equal(booking.Id, payment.BookingId);
        Assert.Equal("Cancelled", payment.BookingStatus);
        Assert.Null(payment.Payment);

        // REGRESSION — The customer still reads their own Booking
        // through the original, non-administrative contract.
        GetBookingResponse customerBooking =
            await GetJsonAsync<GetBookingResponse>(
                customer,
                $"/api/v1/bookings/{booking.Id}");

        Assert.Equal(booking.Id, customerBooking.Id);
        Assert.Equal("Cancelled", customerBooking.Status);
        Assert.Equal(
            "RejectedByOwner",
            customerBooking.CancellationReason);
    }

    [Fact]
    public async Task ConcurrentApproveAndReject_ShouldNotBothSucceed()
    {
        // ARRANGE
        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingApproval,
                cancellationToken: CancellationToken);

        using HttpClient owner =
            _factory.CreateOwnerClient();

        using HttpClient admin =
            _factory.CreateAdminClient();

        string bookingUrl =
            $"/api/v1/admin/properties/{booking.PropertyId}" +
            $"/bookings/{booking.Id}";

        // ACT — Two incompatible operations are dispatched
        // without waiting for either to complete first.
        Task<HttpResponseMessage> approveTask =
            owner.PostAsync(
                $"{bookingUrl}/approve",
                content: null,
                CancellationToken);

        Task<HttpResponseMessage> rejectTask =
            admin.PostAsync(
                $"{bookingUrl}/reject",
                content: null,
                CancellationToken);

        HttpResponseMessage[] responses =
            await Task.WhenAll(
                approveTask,
                rejectTask);

        using HttpResponseMessage approveResponse =
            responses[0];

        using HttpResponseMessage rejectResponse =
            responses[1];

        //int successCount = responses.Count(
        //    response =>
        //        response.StatusCode == HttpStatusCode.NoContent);

        //int conflictCount = responses.Count(
        //    response =>
        //        response.StatusCode == HttpStatusCode.Conflict);

        //// ASSERT — Exactly one operation must succeed.
        //Assert.Equal(1, successCount);
        //Assert.Equal(1, conflictCount);
        int successCount = responses.Count(
    response =>
        response.StatusCode == HttpStatusCode.NoContent);

        int conflictCount = responses.Count(
            response =>
                response.StatusCode == HttpStatusCode.Conflict);

        string approveBody =
            await approveResponse.Content.ReadAsStringAsync(CancellationToken);

        string rejectBody =
            await rejectResponse.Content.ReadAsStringAsync(CancellationToken);

        // ASSERT — Exactly one operation must succeed.
        Assert.True(
            successCount == 1 && conflictCount == 1,
            $"""
    Unexpected concurrent operation results.

    Approve:
      Status: {(int)approveResponse.StatusCode} ({approveResponse.StatusCode})
      Body: {approveBody}

    Reject:
      Status: {(int)rejectResponse.StatusCode} ({rejectResponse.StatusCode})
      Body: {rejectBody}

    SuccessCount: {successCount}
    ConflictCount: {conflictCount}
    """);

        // Verify the persisted Booking reflects the winner.
        AdminBookingDetailsResponse persisted =
            await GetJsonAsync<AdminBookingDetailsResponse>(
                admin,
                bookingUrl);

        if (approveResponse.StatusCode == HttpStatusCode.NoContent)
        {
            Assert.Equal(
                "PendingPayment",
                persisted.Status);

            Assert.NotNull(
                persisted.ApprovedAtUtc);
        }
        else
        {
            Assert.Equal(
                HttpStatusCode.NoContent,
                rejectResponse.StatusCode);

            Assert.Equal(
                "Cancelled",
                persisted.Status);

            Assert.Equal(
                "RejectedByOwner",
                persisted.CancellationReason);
        }
    }

    private static async Task<T> GetJsonAsync<T>(
        HttpClient client,
        string url)
    {
        using HttpResponseMessage response =
            await client.GetAsync(
                url,
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        return Assert.IsType<T>(
            await response.Content.ReadFromJsonAsync<T>(
                CancellationToken));
    }
}
