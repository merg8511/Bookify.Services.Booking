
using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Administrative.Bookings.Calendar;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class AdminBookingCalendarEndpointTests
{
    private readonly BookingApiFactory _factory;

    private static CancellationToken CancellationToken =>
        TestContext.Current.CancellationToken;

    public AdminBookingCalendarEndpointTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    private static string CalendarUrl(
        Guid propertyId,
        string queryString)
    {
        return
            $"/api/v1/admin/properties/{propertyId}/calendar" +
            $"?{queryString}";
    }

    [Fact]
    public async Task Get_WithUnitAndBlockingFilters_ShouldReturnOnlyMatchingBookings()
    {
        // ARRANGE
        var booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.Paid,
                cancellationToken: CancellationToken);

        // This booking belongs to a different property.
        // It must never appear in the requested calendar.
        var otherBooking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.Paid,
                cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        string endpoint = CalendarUrl(
            booking.PropertyId,
            "rangeStart=2026-09-11" +
            "&rangeEnd=2026-09-13" +
            $"&unitId={booking.RentableUnitId}" +
            "&blocksInventory=true");

        // ACT
        using HttpResponseMessage response =
            await owner.GetAsync(
                endpoint,
                CancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        GetAdminBookingCalendarResponse calendar =
            Assert.IsType<GetAdminBookingCalendarResponse>(
                await response.Content.ReadFromJsonAsync<
                    GetAdminBookingCalendarResponse>(
                    CancellationToken));

        Assert.Equal(booking.PropertyId, calendar.PropertyId);

        Assert.Equal(
            new DateOnly(2026, 9, 11),
            calendar.RangeStart);

        Assert.Equal(
            new DateOnly(2026, 9, 13),
            calendar.RangeEnd);

        AdminBookingCalendarItemResponse item =
            Assert.Single(calendar.Items);

        Assert.Equal(booking.Id, item.BookingId);
        Assert.Equal(
            booking.RentableUnitId,
            item.RentableUnitId);

        Assert.Equal("Paid", item.Status);
        Assert.True(item.BlocksInventory);
        Assert.Equal(2, item.GuestCount);

        Assert.DoesNotContain(
            calendar.Items,
            item => item.BookingId == otherBooking.Id);

        // ACT — Opposite blocking filter.
        using HttpResponseMessage nonBlockingResponse =
            await owner.GetAsync(
                CalendarUrl(
                    booking.PropertyId,
                    "rangeStart=2026-09-11" +
                    "&rangeEnd=2026-09-13" +
                    "&blocksInventory=false"),
                CancellationToken);

        GetAdminBookingCalendarResponse nonBlockingCalendar =
            Assert.IsType<GetAdminBookingCalendarResponse>(
                await nonBlockingResponse.Content.ReadFromJsonAsync<
                    GetAdminBookingCalendarResponse>(
                    CancellationToken));

        Assert.Empty(nonBlockingCalendar.Items);

        // ACT — Check-out date is exclusive.
        using HttpResponseMessage outsideRangeResponse =
            await owner.GetAsync(
                CalendarUrl(
                    booking.PropertyId,
                    "rangeStart=2026-09-12" +
                    "&rangeEnd=2026-09-13"),
                CancellationToken);

        GetAdminBookingCalendarResponse outsideRangeCalendar =
            Assert.IsType<GetAdminBookingCalendarResponse>(
                await outsideRangeResponse.Content.ReadFromJsonAsync<
                    GetAdminBookingCalendarResponse>(
                    CancellationToken));

        Assert.Empty(outsideRangeCalendar.Items);
    }

    [Fact]
    public async Task Get_CancelledBooking_ShouldReturnNonBlockingItem()
    {
        // ARRANGE
        var booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.Cancelled,
                cancellationReason:
                    BookingCancellationReason.RejectedByOwner,
                cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        string endpoint = CalendarUrl(
            booking.PropertyId,
            "rangeStart=2026-09-10" +
            "&rangeEnd=2026-09-12" +
            "&blocksInventory=false");

        // ACT
        using HttpResponseMessage response =
            await owner.GetAsync(
                endpoint,
                CancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        GetAdminBookingCalendarResponse calendar =
            Assert.IsType<GetAdminBookingCalendarResponse>(
                await response.Content.ReadFromJsonAsync<
                    GetAdminBookingCalendarResponse>(
                    CancellationToken));

        AdminBookingCalendarItemResponse item =
            Assert.Single(calendar.Items);

        Assert.Equal(booking.Id, item.BookingId);
        Assert.Equal("Cancelled", item.Status);
        Assert.False(item.BlocksInventory);
    }

    [Fact]
    public async Task Get_ShouldEnforcePropertyOwnership()
    {
        // ARRANGE
        var booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                cancellationToken: CancellationToken);

        string endpoint = CalendarUrl(
            booking.PropertyId,
            "rangeStart=2026-09-10&rangeEnd=2026-09-12");

        using HttpClient owner = _factory.CreateOwnerClient();

        using HttpClient anotherOwner =
            _factory.CreateOwnerClient("different-owner");

        using HttpClient customer =
            _factory.CreateCustomerClient();

        using HttpClient admin =
            _factory.CreateAdminClient();

        // ACT
        using HttpResponseMessage ownerResponse =
            await owner.GetAsync(
                endpoint,
                CancellationToken);

        using HttpResponseMessage anotherOwnerResponse =
            await anotherOwner.GetAsync(
                endpoint,
                CancellationToken);

        using HttpResponseMessage customerResponse =
            await customer.GetAsync(
                endpoint,
                CancellationToken);

        using HttpResponseMessage anonymousResponse =
            await _factory.Client.GetAsync(
                endpoint,
                CancellationToken);

        using HttpResponseMessage adminResponse =
            await admin.GetAsync(
                endpoint,
                CancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.OK,
            ownerResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            anotherOwnerResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            customerResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            anonymousResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            adminResponse.StatusCode);
    }

    [Fact]
    public async Task Get_WithInvalidRanges_ShouldReturnValidationProblems()
    {
        // ARRANGE
        var seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient admin = _factory.CreateAdminClient();

        // ACT — Missing RangeEnd.
        using HttpResponseMessage missingRange =
            await admin.GetAsync(
                CalendarUrl(
                    seed.PropertyId,
                    "rangeStart=2026-09-10"),
                CancellationToken);

        // ACT — Reversed date range.
        using HttpResponseMessage reversedRange =
            await admin.GetAsync(
                CalendarUrl(
                    seed.PropertyId,
                    "rangeStart=2026-09-12&rangeEnd=2026-09-10"),
                CancellationToken);

        // ACT — More than 366 days.
        using HttpResponseMessage oversizedRange =
            await admin.GetAsync(
                CalendarUrl(
                    seed.PropertyId,
                    "rangeStart=2026-01-01&rangeEnd=2027-01-03"),
                CancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.BadRequest,
            missingRange.StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            reversedRange.StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            oversizedRange.StatusCode);

        ProblemDetailsResponse missingProblem =
            Assert.IsType<ProblemDetailsResponse>(
                await missingRange.Content.ReadFromJsonAsync<
                    ProblemDetailsResponse>(
                    CancellationToken));

        ProblemDetailsResponse reversedProblem =
            Assert.IsType<ProblemDetailsResponse>(
                await reversedRange.Content.ReadFromJsonAsync<
                    ProblemDetailsResponse>(
                    CancellationToken));

        ProblemDetailsResponse oversizedProblem =
            Assert.IsType<ProblemDetailsResponse>(
                await oversizedRange.Content.ReadFromJsonAsync<
                    ProblemDetailsResponse>(
                    CancellationToken));

        Assert.Equal(
            "AdminCalendar.InvalidDateRange",
            missingProblem.Code);

        Assert.Equal(
            "AdminCalendar.InvalidDateRange",
            reversedProblem.Code);

        Assert.Equal(
            "AdminCalendar.RangeTooLarge",
            oversizedProblem.Code);
    }
}
