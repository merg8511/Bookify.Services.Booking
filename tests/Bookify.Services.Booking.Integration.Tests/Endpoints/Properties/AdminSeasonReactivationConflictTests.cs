
using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits.Seasons;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Properties;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class AdminSeasonReactivationConflictTests
{
    private readonly BookingApiFactory _factory;

    private static CancellationToken CancellationToken =>
        TestContext.Current.CancellationToken;

    public AdminSeasonReactivationConflictTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Reactivate_WithSamePriorityOverlap_ShouldRejectAndPreserveState()
    {
        // ARRANGE
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient owner =
            _factory.CreateOwnerClient();

        string baseUrl =
            $"/api/v1/admin/properties/{seed.PropertyId}" +
            $"/units/{seed.RentableUnitId}/seasons";

        // First season: [Dec 20, Dec 24), priority 10.
        int firstSeasonId = await CreateSeasonAsync(
            owner,
            baseUrl,
            new DateOnly(2026, 12, 20),
            new DateOnly(2026, 12, 24),
            nightlyRate: 180m,
            priority: 10);

        // Second season: [Dec 24, Dec 28), same priority.
        // They are adjacent, so this is initially valid.
        int secondSeasonId = await CreateSeasonAsync(
            owner,
            baseUrl,
            new DateOnly(2026, 12, 24),
            new DateOnly(2026, 12, 28),
            nightlyRate: 190m,
            priority: 10);

        string secondUrl =
            $"{baseUrl}/{secondSeasonId}";

        // Deactivate the second season.
        using HttpResponseMessage deactivateResponse =
            await owner.PatchAsJsonAsync(
                $"{secondUrl}/status",
                new SetPricingSeasonStatusRequest(false),
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.NoContent,
            deactivateResponse.StatusCode);

        // Update its dates while inactive.
        // It now overlaps the first season.
        var updateRequest = new UpdatePricingSeasonRequest(
            StartDate: new DateOnly(2026, 12, 22),
            EndDate: new DateOnly(2026, 12, 28),
            NightlyRate: 190m,
            Currency: "USD",
            Priority: 10);

        using HttpResponseMessage updateResponse =
            await owner.PutAsJsonAsync(
                secondUrl,
                updateRequest,
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.NoContent,
            updateResponse.StatusCode);

        // ACT — Reactivation would introduce ambiguity.
        using HttpResponseMessage reactivateResponse =
            await owner.PatchAsJsonAsync(
                $"{secondUrl}/status",
                new SetPricingSeasonStatusRequest(true),
                CancellationToken);

        // ASSERT — Domain must reject the transition.
        Assert.Equal(
            HttpStatusCode.Conflict,
            reactivateResponse.StatusCode);

        ProblemDetailsResponse problem =
            Assert.IsType<ProblemDetailsResponse>(
                await reactivateResponse.Content
                    .ReadFromJsonAsync<ProblemDetailsResponse>(
                        CancellationToken));

        Assert.Equal(
            "PricingSeason.AmbiguousPriority",
            problem.Code);

        // Verify that the failed reactivation did not mutate DB state.
        using HttpResponseMessage detailResponse =
            await owner.GetAsync(
                secondUrl,
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            detailResponse.StatusCode);

        AdminPricingSeasonResponse persisted =
            Assert.IsType<AdminPricingSeasonResponse>(
                await detailResponse.Content
                    .ReadFromJsonAsync<AdminPricingSeasonResponse>(
                        CancellationToken));

        Assert.Equal(secondSeasonId, persisted.Id);
        Assert.False(persisted.IsActive);

        Assert.Equal(
            new DateOnly(2026, 12, 22),
            persisted.StartDate);

        Assert.Equal(
            new DateOnly(2026, 12, 28),
            persisted.EndDate);

        Assert.Equal(190m, persisted.NightlyRate);

        // The first season remains active and unchanged.
        using HttpResponseMessage firstDetailResponse =
            await owner.GetAsync(
                $"{baseUrl}/{firstSeasonId}",
                CancellationToken);

        AdminPricingSeasonResponse firstPersisted =
            Assert.IsType<AdminPricingSeasonResponse>(
                await firstDetailResponse.Content
                    .ReadFromJsonAsync<AdminPricingSeasonResponse>(
                        CancellationToken));

        Assert.True(firstPersisted.IsActive);
        Assert.Equal(180m, firstPersisted.NightlyRate);
    }

    private static async Task<int> CreateSeasonAsync(
        HttpClient client,
        string baseUrl,
        DateOnly startDate,
        DateOnly endDate,
        decimal nightlyRate,
        int priority)
    {
        var request = new CreatePricingSeasonRequest(
            startDate,
            endDate,
            nightlyRate,
            "USD",
            priority);

        using HttpResponseMessage response =
            await client.PostAsJsonAsync(
                baseUrl,
                request,
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        CreatePricingSeasonResponse created =
            Assert.IsType<CreatePricingSeasonResponse>(
                await response.Content
                    .ReadFromJsonAsync<CreatePricingSeasonResponse>(
                        CancellationToken));

        Assert.True(created.Id > 0);

        return created.Id;
    }
}
