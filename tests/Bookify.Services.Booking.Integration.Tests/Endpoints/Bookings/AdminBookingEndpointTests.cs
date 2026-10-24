
using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Contracts.Pagination;
using Bookify.Services.Booking.Api.Endpoints.Administrative.Bookings;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class AdminBookingEndpointTests
{
    private readonly BookingApiFactory _factory;

    private static CancellationToken CancellationToken =>
        TestContext.Current.CancellationToken;

    public AdminBookingEndpointTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    private static string BookingsUrl(Guid propertyId) =>
        $"/api/v1/admin/properties/{propertyId}/bookings";

    private static string BookingUrl(
        Guid propertyId,
        Guid bookingId) =>
        $"{BookingsUrl(propertyId)}/{bookingId}";

    [Fact]
    public async Task GetPaged_WithFilters_ShouldReturnMatchingBookings()
    {
        // ARRANGE
        var booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory,
            status: BookingStatus.Paid,
            cancellationToken: CancellationToken);

        // Another property must not leak into the current result.
        await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory,
            status: BookingStatus.Paid,
            cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        string customerSubjectId = Uri.EscapeDataString(
            TestIdentitySubjects.Customer);

        string endpoint =
            $"{BookingsUrl(booking.PropertyId)}" +
            "?pageNumber=1" +
            "&pageSize=10" +
            "&status=Paid" +
            "&rangeStart=2026-09-11" +
            "&rangeEnd=2026-09-13" +
            $"&unitId={booking.RentableUnitId}" +
            $"&customerSubjectId={customerSubjectId}";

        // ACT
        using HttpResponseMessage response =
            await owner.GetAsync(
                endpoint,
                CancellationToken);

        // ASSERT
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        PagedResponse<AdminBookingListItemResponse> page =
            Assert.IsType<PagedResponse<AdminBookingListItemResponse>>(
                await response.Content.ReadFromJsonAsync<
                    PagedResponse<AdminBookingListItemResponse>>(
                    CancellationToken));

        Assert.Equal(1, page.PageNumber);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(1, page.TotalRecords);
        Assert.Equal(1, page.TotalPages);

        AdminBookingListItemResponse item =
            Assert.Single(page.Items);

        Assert.Equal(booking.Id, item.Id);
        Assert.Equal(booking.PropertyId, item.PropertyId);
        Assert.Equal(booking.RentableUnitId, item.RentableUnitId);
        Assert.Equal(booking.Reference.Value, item.BookingReference);
        Assert.Equal("Paid", item.Status);
        Assert.Equal(
            TestIdentitySubjects.Customer,
            item.CustomerSubjectId);
    }

    [Fact]
    public async Task GetById_AsOwner_ShouldReturnBookingDetails()
    {
        // ARRANGE
        var booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory,
            status: BookingStatus.Paid,
            cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        // ACT
        using HttpResponseMessage response =
            await owner.GetAsync(
                BookingUrl(booking.PropertyId, booking.Id),
                CancellationToken);

        // ASSERT
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AdminBookingDetailsResponse details =
            Assert.IsType<AdminBookingDetailsResponse>(
                await response.Content.ReadFromJsonAsync<
                    AdminBookingDetailsResponse>(
                    CancellationToken));

        Assert.Equal(booking.Id, details.Id);
        Assert.Equal(booking.PropertyId, details.PropertyId);
        Assert.Equal(booking.RentableUnitId, details.RentableUnitId);
        Assert.Equal(booking.Reference.Value, details.BookingReference);
        Assert.Equal("Paid", details.Status);

        Assert.Equal(
            TestIdentitySubjects.Customer,
            details.CustomerSubjectId);

        Assert.False(string.IsNullOrWhiteSpace(details.GuestFullName));
        Assert.False(string.IsNullOrWhiteSpace(details.GuestEmail));

        Assert.NotNull(details.TotalPrice);
        Assert.Equal("USD", details.Currency);
    }

    [Fact]
    public async Task GetPaged_ShouldEnforcePropertyOwnership()
    {
        // ARRANGE
        var booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory,
            cancellationToken: CancellationToken);

        string endpoint = BookingsUrl(booking.PropertyId);

        using HttpClient otherOwner =
            _factory.CreateOwnerClient("different-owner");

        using HttpClient customer =
            _factory.CreateCustomerClient();

        using HttpClient admin =
            _factory.CreateAdminClient();

        // ACT — Anonymous
        using HttpResponseMessage anonymousResponse =
            await _factory.Client.GetAsync(
                endpoint,
                CancellationToken);

        // ACT — Wrong Owner
        using HttpResponseMessage wrongOwnerResponse =
            await otherOwner.GetAsync(
                endpoint,
                CancellationToken);

        // ACT — Customer
        using HttpResponseMessage customerResponse =
            await customer.GetAsync(
                endpoint,
                CancellationToken);

        // ACT — Admin
        using HttpResponseMessage adminResponse =
            await admin.GetAsync(
                endpoint,
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
            HttpStatusCode.OK,
            adminResponse.StatusCode);
    }

    [Fact]
    public async Task GetById_WithBookingFromAnotherProperty_ShouldReturnNotFound()
    {
        // ARRANGE
        var booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory,
            cancellationToken: CancellationToken);

        SeedData secondProperty =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient admin = _factory.CreateAdminClient();

        // ACT
        using HttpResponseMessage response =
            await admin.GetAsync(
                BookingUrl(secondProperty.PropertyId, booking.Id),
                CancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        ProblemDetailsResponse problem =
            Assert.IsType<ProblemDetailsResponse>(
                await response.Content.ReadFromJsonAsync<
                    ProblemDetailsResponse>(
                    CancellationToken));

        Assert.Equal("Booking.NotFound", problem.Code);
    }

    [Fact]
    public async Task GetPaged_WithInvalidFilters_ShouldReturnValidationProblems()
    {
        // ARRANGE
        SeedData property =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient admin = _factory.CreateAdminClient();

        string baseUrl = BookingsUrl(property.PropertyId);

        // ACT — Invalid status.
        using HttpResponseMessage invalidStatus =
            await admin.GetAsync(
                $"{baseUrl}?status=Unknown",
                CancellationToken);

        // ACT — Incomplete range.
        using HttpResponseMessage incompleteRange =
            await admin.GetAsync(
                $"{baseUrl}?rangeStart=2026-09-10",
                CancellationToken);

        // ACT — Invalid pagination.
        using HttpResponseMessage invalidPagination =
            await admin.GetAsync(
                $"{baseUrl}?pageSize=101",
                CancellationToken);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.BadRequest,
            invalidStatus.StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            incompleteRange.StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            invalidPagination.StatusCode);

        ProblemDetailsResponse statusProblem =
            Assert.IsType<ProblemDetailsResponse>(
                await invalidStatus.Content.ReadFromJsonAsync<
                    ProblemDetailsResponse>(
                    CancellationToken));

        ProblemDetailsResponse rangeProblem =
            Assert.IsType<ProblemDetailsResponse>(
                await incompleteRange.Content.ReadFromJsonAsync<
                    ProblemDetailsResponse>(
                    CancellationToken));

        ProblemDetailsResponse paginationProblem =
            Assert.IsType<ProblemDetailsResponse>(
                await invalidPagination.Content.ReadFromJsonAsync<
                    ProblemDetailsResponse>(
                    CancellationToken));

        Assert.Equal(
            "AdminBookings.InvalidStatus",
            statusProblem.Code);

        Assert.Equal(
            "AdminBookings.InvalidDateRange",
            rangeProblem.Code);

        Assert.Equal(
            "Pagination.PageSizeExceeded",
            paginationProblem.Code);
    }
}
