using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits.Seasons;
using Bookify.Services.Booking.Application.Availability;
using Bookify.Services.Booking.Application.Availability.ReadModels;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Properties;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class AdminPricingSeasonEndpointTests
{
    private readonly BookingApiFactory _factory;

    private static CancellationToken CancellationToken =>
            TestContext.Current.CancellationToken;

    public AdminPricingSeasonEndpointTests(
            BookingApiFactory factory)
    {
        _factory = factory;
    }

    private static string SeasonsUrl(
            Guid propertyId,
            Guid unitId) =>
            $"/api/v1/admin/properties/{propertyId}/units/{unitId}/seasons";

    private static string SeasonUrl(
            Guid propertyId,
            Guid unitId,
            int seasonId) =>
            $"{SeasonsUrl(propertyId, unitId)}/{seasonId}";

    [Fact]
    public async Task CreateUpdateDeactivateAndReactivate_ShouldPersistSeason()
    {
        // ARRANGE
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        string collectionUrl = SeasonsUrl(
                    seed.PropertyId,
                    seed.RentableUnitId);

        var createRequest = new CreatePricingSeasonRequest(
                    StartDate: new DateOnly(2026, 12, 20),
                    EndDate: new DateOnly(2026, 12, 30),
                    NightlyRate: 180m,
                    Currency: "USD",
                    Priority: 10);

        // ACT 1 — Create.
        using HttpResponseMessage createResponse =
            await owner.PostAsJsonAsync(
                collectionUrl,
                createRequest,
                CancellationToken);

        Assert.Equal(
                    HttpStatusCode.Created,
                    createResponse.StatusCode);

        CreatePricingSeasonResponse created =
                    Assert.IsType<CreatePricingSeasonResponse>(
                        await createResponse.Content
                            .ReadFromJsonAsync<CreatePricingSeasonResponse>(
                                CancellationToken));

        Assert.True(created.Id > 0);

        string detailUrl = SeasonUrl(
                    seed.PropertyId,
                    seed.RentableUnitId,
                    created.Id);

        Assert.NotNull(createResponse.Headers.Location);

        Assert.EndsWith(
                    detailUrl,
                    createResponse.Headers.Location.ToString(),
                    StringComparison.Ordinal);

        // ACT 2 — Update.
        var updateRequest = new UpdatePricingSeasonRequest(
            StartDate: new DateOnly(2026, 12, 21),
            EndDate: new DateOnly(2026, 12, 29),
            NightlyRate: 225m,
            Currency: "USD",
            Priority: 15);

        using HttpResponseMessage updateResponse =
                    await owner.PutAsJsonAsync(
                        detailUrl,
                        updateRequest,
                        CancellationToken);

        Assert.Equal(
                    HttpStatusCode.NoContent,
                    updateResponse.StatusCode);

        // ACT 3 — Read persisted detail.
        using HttpResponseMessage detailResponse =
            await owner.GetAsync(
                detailUrl,
                CancellationToken);

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

        AdminPricingSeasonResponse persisted =
                    Assert.IsType<AdminPricingSeasonResponse>(
                        await detailResponse.Content
                            .ReadFromJsonAsync<AdminPricingSeasonResponse>(
                                CancellationToken));

        Assert.Equal(created.Id, persisted.Id);
        Assert.Equal(updateRequest.StartDate, persisted.StartDate);
        Assert.Equal(updateRequest.EndDate, persisted.EndDate);
        Assert.Equal(225m, persisted.NightlyRate);
        Assert.Equal("USD", persisted.Currency);
        Assert.Equal(15, persisted.Priority);
        Assert.True(persisted.IsActive);

        // ACT 4 — Deactivate.
        using HttpResponseMessage deactivateResponse =
            await owner.PatchAsJsonAsync(
                $"{detailUrl}/status",
                new SetPricingSeasonStatusRequest(false),
                CancellationToken);

        Assert.Equal(
                    HttpStatusCode.NoContent,
                    deactivateResponse.StatusCode);

        // Verify administrative reads preserve inactive seasons.
        using HttpResponseMessage listResponse =
            await owner.GetAsync(
                collectionUrl,
                CancellationToken);

        GetAdminPricingSeasonsResponse list =
                    Assert.IsType<GetAdminPricingSeasonsResponse>(
                        await listResponse.Content
                            .ReadFromJsonAsync<GetAdminPricingSeasonsResponse>(
                                CancellationToken));

        AdminPricingSeasonResponse inactive =
                    Assert.Single(
                        list.Seasons,
                        season => season.Id == created.Id);

        Assert.False(inactive.IsActive);

        // Verify the public pricing read side excludes inactive seasons.
        await using (AsyncServiceScope scope =
            _factory.Services.CreateAsyncScope())
        {
            IAvailabilityReadService availabilityReadService =
                scope.ServiceProvider
                    .GetRequiredService<IAvailabilityReadService>();

            IReadOnlyList<AvailabilityPricingSeasonReadModel> publicSeasons =
                            await availabilityReadService.GetPricingSeasonsAsync(
                                [seed.RentableUnitId],
                                new DateOnly(2026, 12, 21),
                                new DateOnly(2026, 12, 29),
                                CancellationToken);

            Assert.DoesNotContain(
                            publicSeasons,
                            season => season.RentableUnitId == seed.RentableUnitId);
        }

        // ACT 5 — Reactivate.
        using HttpResponseMessage reactivateResponse =
            await owner.PatchAsJsonAsync(
                $"{detailUrl}/status",
                new SetPricingSeasonStatusRequest(true),
                CancellationToken);

        Assert.Equal(
                    HttpStatusCode.NoContent,
                    reactivateResponse.StatusCode);

        using HttpResponseMessage activeResponse =
                    await owner.GetAsync(
                        detailUrl,
                        CancellationToken);

        AdminPricingSeasonResponse active =
                    Assert.IsType<AdminPricingSeasonResponse>(
                        await activeResponse.Content
                            .ReadFromJsonAsync<AdminPricingSeasonResponse>(
                                CancellationToken));

        Assert.True(active.IsActive);
    }

    [Fact]
    public async Task Create_WithSamePriorityOverlap_ShouldReturnConflict()
    {
        // ARRANGE
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        string endpoint = SeasonsUrl(
                    seed.PropertyId,
                    seed.RentableUnitId);

        var firstRequest = new CreatePricingSeasonRequest(
                    new DateOnly(2026, 12, 20),
                    new DateOnly(2026, 12, 30),
                    180m,
                    "USD",
                    10);

        var overlappingRequest = new CreatePricingSeasonRequest(
                    new DateOnly(2026, 12, 24),
                    new DateOnly(2026, 12, 27),
                    250m,
                    "USD",
                    10);

        using HttpResponseMessage firstResponse =
                    await owner.PostAsJsonAsync(
                        endpoint,
                        firstRequest,
                        CancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // ACT
        using HttpResponseMessage response =
            await owner.PostAsJsonAsync(
                endpoint,
                overlappingRequest,
                CancellationToken);

        // ASSERT
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        ProblemDetailsResponse problem =
                    Assert.IsType<ProblemDetailsResponse>(
                        await response.Content
                            .ReadFromJsonAsync<ProblemDetailsResponse>(
                                CancellationToken));

        Assert.Equal(
                    "PricingSeason.AmbiguousPriority",
                    problem.Code);
    }
}
