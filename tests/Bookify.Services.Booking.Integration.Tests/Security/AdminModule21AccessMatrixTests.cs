
using System.Net;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Security;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class AdminModule21AccessMatrixTests
{
    private readonly BookingApiFactory _factory;

    private static CancellationToken CancellationToken =>
        TestContext.Current.CancellationToken;

    public AdminModule21AccessMatrixTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdministrativeEndpoints_ShouldDenyUnauthorizedActors()
    {
        // ARRANGE
        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                status: BookingStatus.PendingApproval,
                cancellationToken: CancellationToken);

        Guid propertyId = booking.PropertyId;
        Guid unitId = booking.RentableUnitId;
        Guid bookingId = booking.Id;

        string propertyUrl =
            $"/api/v1/properties/{propertyId}";

        string adminPropertyUrl =
            $"/api/v1/admin/properties/{propertyId}";

        string unitsUrl =
            $"{adminPropertyUrl}/units";

        string unitUrl =
            $"{unitsUrl}/{unitId}";

        string seasonsUrl =
            $"{unitUrl}/seasons";

        string seasonUrl =
            $"{seasonsUrl}/1";

        string bookingsUrl =
            $"{adminPropertyUrl}/bookings";

        string bookingUrl =
            $"{bookingsUrl}/{bookingId}";

        RouteCase[] routes =
        [
            // 21.1 — Property
            new("UpdateProperty", HttpMethod.Put, propertyUrl),
            new("SetPropertyStatus", HttpMethod.Patch,
                $"{propertyUrl}/status"),

            // 21.2 — RentableUnit
            new("CreateUnit", HttpMethod.Post, unitsUrl),
            new("ListUnits", HttpMethod.Get, unitsUrl),
            new("GetUnit", HttpMethod.Get, unitUrl),
            new("UpdateUnit", HttpMethod.Put, unitUrl),
            new("SetUnitStatus", HttpMethod.Patch,
                $"{unitUrl}/status"),

            // 21.3 — Pricing
            new("GetPricing", HttpMethod.Get,
                $"{unitUrl}/pricing"),
            new("ConfigurePricing", HttpMethod.Put,
                $"{unitUrl}/pricing"),

            // 21.4 — Seasons
            new("CreateSeason", HttpMethod.Post, seasonsUrl),
            new("ListSeasons", HttpMethod.Get, seasonsUrl),
            new("GetSeason", HttpMethod.Get, seasonUrl),
            new("UpdateSeason", HttpMethod.Put, seasonUrl),
            new("SetSeasonStatus", HttpMethod.Patch,
                $"{seasonUrl}/status"),

            // 21.5 — Booking reads
            new("ListBookings", HttpMethod.Get, bookingsUrl),
            new("GetBooking", HttpMethod.Get, bookingUrl),

            // 21.6 — Calendar
            new("GetCalendar", HttpMethod.Get,
                $"{adminPropertyUrl}/calendar" +
                "?rangeStart=2026-09-10&rangeEnd=2026-09-12"),

            // 21.7 — Actions
            new("ApproveBooking", HttpMethod.Post,
                $"{bookingUrl}/approve"),
            new("RejectBooking", HttpMethod.Post,
                $"{bookingUrl}/reject"),

            // 21.8 — Payment administration
            new("GetPayment", HttpMethod.Get,
                $"{bookingUrl}/payment")
        ];

        using HttpClient customer =
            _factory.CreateCustomerClient();

        using HttpClient differentOwner =
            _factory.CreateOwnerClient("different-owner");

        GuestBookingSeedData guest =
            await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient guestClient =
            _factory.CreateGuestClient(guest.GuestAccessToken);

        foreach (RouteCase route in routes)
        {
            // Customer must not obtain administrative access.
            await AssertStatusAsync(
                customer,
                route,
                HttpStatusCode.Forbidden);

            // Owner of a different property is denied.
            await AssertStatusAsync(
                differentOwner,
                route,
                HttpStatusCode.Forbidden);

            // Guest credential does not grant Owner/Admin rights.
            await AssertStatusAsync(
                guestClient,
                route,
                HttpStatusCode.Unauthorized);

            // Anonymous caller is challenged.
            await AssertStatusAsync(
                _factory.Client,
                route,
                HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task OwnerAndAdmin_ShouldReadTheirAuthorizedResources()
    {
        // ARRANGE
        DomainBooking booking =
            await BookingDatabaseTestSeeder.SeedBookingAsync(
                _factory,
                cancellationToken: CancellationToken);

        Guid propertyId = booking.PropertyId;
        Guid unitId = booking.RentableUnitId;
        Guid bookingId = booking.Id;

        string adminPropertyUrl =
            $"/api/v1/admin/properties/{propertyId}";

        string unitsUrl =
            $"{adminPropertyUrl}/units";

        string bookingsUrl =
            $"{adminPropertyUrl}/bookings";

        string[] urls =
        [
            unitsUrl,
            $"{unitsUrl}/{unitId}",
            $"{unitsUrl}/{unitId}/pricing",
            $"{unitsUrl}/{unitId}/seasons",
            bookingsUrl,
            $"{bookingsUrl}/{bookingId}",
            $"{bookingsUrl}/{bookingId}/payment",
            $"{adminPropertyUrl}/calendar" +
            "?rangeStart=2026-09-10&rangeEnd=2026-09-12"
        ];

        using HttpClient owner =
            _factory.CreateOwnerClient();

        using HttpClient admin =
            _factory.CreateAdminClient();

        // ACT / ASSERT
        foreach (string url in urls)
        {
            using HttpResponseMessage ownerResponse =
                await owner.GetAsync(
                    url,
                    CancellationToken);

            Assert.True(
                ownerResponse.StatusCode == HttpStatusCode.OK,
                $"Owner failed for {url}: " +
                $"{(int)ownerResponse.StatusCode}");

            using HttpResponseMessage adminResponse =
                await admin.GetAsync(
                    url,
                    CancellationToken);

            Assert.True(
                adminResponse.StatusCode == HttpStatusCode.OK,
                $"Admin failed for {url}: " +
                $"{(int)adminResponse.StatusCode}");
        }
    }

    private static async Task AssertStatusAsync(
        HttpClient client,
        RouteCase route,
        HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(
            route.Method,
            route.Url);

        using HttpResponseMessage response =
            await client.SendAsync(
                request,
                CancellationToken);

        Assert.True(
            response.StatusCode == expected,
            $"{route.Name}: expected {(int)expected}, " +
            $"received {(int)response.StatusCode}. " +
            $"Route: {route.Method} {route.Url}");
    }

    private sealed record RouteCase(
        string Name,
        HttpMethod Method,
        string Url);
}
