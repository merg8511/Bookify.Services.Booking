
using System.Net;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Properties;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class AdminResourceIsolationTests
{
    private readonly BookingApiFactory _factory;

    private static CancellationToken CancellationToken =>
        TestContext.Current.CancellationToken;

    public AdminResourceIsolationTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnitResources_FromAnotherProperty_ShouldReturnNotFound()
    {
        // ARRANGE
        SeedData firstProperty =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        SeedData secondProperty =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient owner =
            _factory.CreateOwnerClient();

        using HttpClient admin =
            _factory.CreateAdminClient();

        // The same Owner can manage both properties,
        // but the URL must still match the unit's PropertyId.
        string foreignUnitUrl =
            $"/api/v1/admin/properties/{firstProperty.PropertyId}" +
            $"/units/{secondProperty.RentableUnitId}";

        string[] foreignResourceUrls =
        [
            foreignUnitUrl,
            $"{foreignUnitUrl}/pricing",
            $"{foreignUnitUrl}/seasons"
        ];

        // ACT / ASSERT
        foreach (string url in foreignResourceUrls)
        {
            using HttpResponseMessage ownerResponse =
                await owner.GetAsync(
                    url,
                    CancellationToken);

            using HttpResponseMessage adminResponse =
                await admin.GetAsync(
                    url,
                    CancellationToken);

            Assert.Equal(
                HttpStatusCode.NotFound,
                ownerResponse.StatusCode);

            Assert.Equal(
                HttpStatusCode.NotFound,
                adminResponse.StatusCode);
        }
    }

    [Fact]
    public async Task UnitResources_FromCorrectProperty_ShouldRemainReadable()
    {
        // ARRANGE
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient owner =
            _factory.CreateOwnerClient();

        string unitUrl =
            $"/api/v1/admin/properties/{seed.PropertyId}" +
            $"/units/{seed.RentableUnitId}";

        // ACT / ASSERT
        foreach (string url in new[]
        {
            unitUrl,
            $"{unitUrl}/pricing",
            $"{unitUrl}/seasons"
        })
        {
            using HttpResponseMessage response =
                await owner.GetAsync(
                    url,
                    CancellationToken);

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);
        }
    }
}
