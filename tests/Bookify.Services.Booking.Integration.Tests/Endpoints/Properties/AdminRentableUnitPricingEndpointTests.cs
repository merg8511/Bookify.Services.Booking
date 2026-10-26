
using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits;
using Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits.Pricing;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Properties;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class AdminRentableUnitPricingEndpointTests
{
    private readonly BookingApiFactory _factory;

    private static CancellationToken CancellationToken =>
        TestContext.Current.CancellationToken;

    public AdminRentableUnitPricingEndpointTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    private static string UnitsUrl(Guid propertyId) =>
        $"/api/v1/admin/properties/{propertyId}/units";

    private static string PricingUrl(
        Guid propertyId,
        Guid unitId) =>
        $"{UnitsUrl(propertyId)}/{unitId}/pricing";

    [Fact]
    public async Task ConfigurePricing_ShouldPersistInitialAndUpdatedRates()
    {
        // ARRANGE
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        // Create a unit without Pricing using the 21.2 API.
        var createRequest = new CreateRentableUnitRequest(
            "Pricing Test Suite",
            "Room",
            MaximumCapacity: 4,
            MaxBaseGuests: 2);

        using HttpResponseMessage createResponse =
            await owner.PostAsJsonAsync(
                UnitsUrl(seed.PropertyId),
                createRequest,
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        CreateRentableUnitResponse created =
            Assert.IsType<CreateRentableUnitResponse>(
                await createResponse.Content
                    .ReadFromJsonAsync<CreateRentableUnitResponse>(
                        CancellationToken));

        string endpoint = PricingUrl(
            seed.PropertyId,
            created.Id);

        // ACT 1 — Read unconfigured pricing.
        using HttpResponseMessage initialGet =
            await owner.GetAsync(
                endpoint,
                CancellationToken);

        Assert.Equal(HttpStatusCode.OK, initialGet.StatusCode);

        GetAdminRentableUnitPricingResponse initial =
            Assert.IsType<GetAdminRentableUnitPricingResponse>(
                await initialGet.Content
                    .ReadFromJsonAsync<GetAdminRentableUnitPricingResponse>(
                        CancellationToken));

        Assert.False(initial.IsConfigured);
        Assert.Null(initial.RegularNightlyRate);
        Assert.Null(initial.WeekendNightlyRate);
        Assert.Null(initial.ExtraGuestNightlyRate);
        Assert.Null(initial.Currency);

        // ACT 2 — Initial configuration.
        var firstRequest = new ConfigureRentableUnitPricingRequest(
            RegularNightlyRate: 100m,
            WeekendNightlyRate: 140m,
            ExtraGuestNightlyRate: 25m,
            Currency: "USD");

        using HttpResponseMessage firstPut =
            await owner.PutAsJsonAsync(
                endpoint,
                firstRequest,
                CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, firstPut.StatusCode);

        // ACT 3 — Replace the existing pricing.
        var secondRequest = new ConfigureRentableUnitPricingRequest(
            RegularNightlyRate: 120.500m,
            WeekendNightlyRate: 180m,
            ExtraGuestNightlyRate: 30m,
            Currency: "USD");

        using HttpResponseMessage secondPut =
            await owner.PutAsJsonAsync(
                endpoint,
                secondRequest,
                CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, secondPut.StatusCode);

        // ASSERT — Dapper reads persisted values in a new request.
        using HttpResponseMessage finalGet =
            await owner.GetAsync(
                endpoint,
                CancellationToken);

        Assert.Equal(HttpStatusCode.OK, finalGet.StatusCode);

        GetAdminRentableUnitPricingResponse pricing =
            Assert.IsType<GetAdminRentableUnitPricingResponse>(
                await finalGet.Content
                    .ReadFromJsonAsync<GetAdminRentableUnitPricingResponse>(
                        CancellationToken));

        Assert.True(pricing.IsConfigured);
        Assert.Equal(seed.PropertyId, pricing.PropertyId);
        Assert.Equal(created.Id, pricing.UnitId);
        Assert.Equal(120.500m, pricing.RegularNightlyRate);
        Assert.Equal(180m, pricing.WeekendNightlyRate);
        Assert.Equal(30m, pricing.ExtraGuestNightlyRate);
        Assert.Equal("USD", pricing.Currency);
    }

    [Fact]
    public async Task ConfigurePricing_WithIncompatibleSeasonCurrency_ShouldReturnConflict()
    {
        // ARRANGE
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                configureUnit: unit =>
                {
                    PricingSeason season = PricingSeason.Create(
                        new DateOnly(2026, 12, 20),
                        new DateOnly(2026, 12, 30),
                        Money.Create(200m, "USD").Value,
                        priority: 10).Value;

                    unit.AddPricingSeason(season);
                },
                cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        string endpoint = PricingUrl(
            seed.PropertyId,
            seed.RentableUnitId);

        var request = new ConfigureRentableUnitPricingRequest(
            RegularNightlyRate: 100m,
            WeekendNightlyRate: 150m,
            ExtraGuestNightlyRate: 25m,
            Currency: "EUR");

        // ACT
        using HttpResponseMessage response =
            await owner.PutAsJsonAsync(
                endpoint,
                request,
                CancellationToken);

        // ASSERT
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        ProblemDetailsResponse problem =
            Assert.IsType<ProblemDetailsResponse>(
                await response.Content
                    .ReadFromJsonAsync<ProblemDetailsResponse>(
                        CancellationToken));

        Assert.Equal(
            "Pricing.SeasonCurrencyMismatch",
            problem.Code);

        // No existing Pricing should have been changed.
        using HttpResponseMessage getResponse =
            await owner.GetAsync(
                endpoint,
                CancellationToken);

        GetAdminRentableUnitPricingResponse pricing =
            Assert.IsType<GetAdminRentableUnitPricingResponse>(
                await getResponse.Content
                    .ReadFromJsonAsync<GetAdminRentableUnitPricingResponse>(
                        CancellationToken));

        Assert.True(pricing.IsConfigured);
        Assert.Equal("USD", pricing.Currency);
        Assert.Equal(100m, pricing.RegularNightlyRate);
        Assert.Equal(140m, pricing.WeekendNightlyRate);
        Assert.Equal(25m, pricing.ExtraGuestNightlyRate);
    }

    [Fact]
    public async Task ConfigurePricing_WithInvalidPrecision_ShouldReturnValidationProblem()
    {
        // ARRANGE
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        var request = new ConfigureRentableUnitPricingRequest(
            RegularNightlyRate: 100.0001m,
            WeekendNightlyRate: 140m,
            ExtraGuestNightlyRate: 25m,
            Currency: "USD");

        // ACT
        using HttpResponseMessage response =
            await owner.PutAsJsonAsync(
                PricingUrl(seed.PropertyId, seed.RentableUnitId),
                request,
                CancellationToken);

        // ASSERT
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        ProblemDetailsResponse problem =
            Assert.IsType<ProblemDetailsResponse>(
                await response.Content
                    .ReadFromJsonAsync<ProblemDetailsResponse>(
                        CancellationToken));

        Assert.Equal("Money.InvalidPrecision", problem.Code);
    }
}
