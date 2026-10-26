
using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits.Seasons;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Administrative;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class AdministrativeAuditRegressionTests
{
    private readonly BookingApiFactory _factory;

    private static CancellationToken CancellationToken =>
        TestContext.Current.CancellationToken;

    public AdministrativeAuditRegressionTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    // ==========================================
    // H2 — Invalid Booking pagination
    // ==========================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetBookings_WithNonPositivePageSize_ShouldReturnBadRequest(
        int pageSize)
    {
        // ARRANGE
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient admin = _factory.CreateAdminClient();

        string url =
            $"/api/v1/admin/properties/{seed.PropertyId}" +
            $"/bookings?pageNumber=1&pageSize={pageSize}";

        // ACT
        using HttpResponseMessage response =
            await admin.GetAsync(url, CancellationToken);

        // ASSERT
        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "Pagination.InvalidPageSize");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetBookings_WithNonPositivePageNumber_ShouldReturnBadRequest(
        int pageNumber)
    {
        // ARRANGE
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient admin = _factory.CreateAdminClient();

        string url =
            $"/api/v1/admin/properties/{seed.PropertyId}" +
            $"/bookings?pageNumber={pageNumber}&pageSize=10";

        // ACT
        using HttpResponseMessage response =
            await admin.GetAsync(url, CancellationToken);

        // ASSERT
        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "Pagination.InvalidPageNumber");
    }

    // ==========================================
    // H1 — Invalid PricingSeason creation
    // ==========================================

    [Fact]
    public async Task CreateSeason_WithInvalidDateRange_ShouldReturnBadRequest()
    {
        var request = new CreatePricingSeasonRequest(
            StartDate: new DateOnly(2026, 12, 30),
            EndDate: new DateOnly(2026, 12, 20),
            NightlyRate: 180m,
            Currency: "USD",
            Priority: 10);

        await AssertInvalidSeasonCreationAsync(
            request,
            "PricingSeason.InvalidDateRange");
    }

    [Fact]
    public async Task CreateSeason_WithNegativePriority_ShouldReturnBadRequest()
    {
        var request = new CreatePricingSeasonRequest(
            StartDate: new DateOnly(2026, 12, 20),
            EndDate: new DateOnly(2026, 12, 30),
            NightlyRate: 180m,
            Currency: "USD",
            Priority: -1);

        await AssertInvalidSeasonCreationAsync(
            request,
            "PricingSeason.InvalidPriority");
    }

    private async Task AssertInvalidSeasonCreationAsync(
        CreatePricingSeasonRequest request,
        string expectedErrorCode)
    {
        // ARRANGE
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        string url =
            $"/api/v1/admin/properties/{seed.PropertyId}" +
            $"/units/{seed.RentableUnitId}/seasons";

        // ACT
        using HttpResponseMessage createResponse =
            await owner.PostAsJsonAsync(
                url,
                request,
                CancellationToken);

        // ASSERT — Correct Problem Details response.
        await AssertProblemAsync(
            createResponse,
            HttpStatusCode.BadRequest,
            expectedErrorCode);

        // ASSERT — Invalid Season was not persisted.
        using HttpResponseMessage listResponse =
            await owner.GetAsync(
                url,
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            listResponse.StatusCode);

        GetAdminPricingSeasonsResponse seasons =
            Assert.IsType<GetAdminPricingSeasonsResponse>(
                await listResponse.Content
                    .ReadFromJsonAsync<GetAdminPricingSeasonsResponse>(
                        CancellationToken));

        Assert.Empty(seasons.Seasons);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        ProblemDetailsResponse problem =
            Assert.IsType<ProblemDetailsResponse>(
                await response.Content
                    .ReadFromJsonAsync<ProblemDetailsResponse>(
                        CancellationToken));

        Assert.Equal(expectedCode, problem.Code);
    }
}
