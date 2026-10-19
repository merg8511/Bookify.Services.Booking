
using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Properties.GetById;
using Bookify.Services.Booking.Api.Endpoints.Properties.SetStatus;
using Bookify.Services.Booking.Api.Endpoints.Properties.Update;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Properties;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class PropertyManagementEndpointSmokeTests
{
    private readonly BookingApiFactory _factory;

    private static CancellationToken CancellationToken =>
        TestContext.Current.CancellationToken;

    public PropertyManagementEndpointSmokeTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpdateProperty_AsOwner_ShouldPersistChanges()
    {
        // Arrange
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient ownerClient = _factory.CreateOwnerClient();

        string endpoint = $"/api/v1/properties/{seed.PropertyId}";

        var request = new UpdatePropertyRequest(
            "Updated Property",
            "America/New_York",
            new TimeOnly(14, 0),
            new TimeOnly(10, 0));

        // Act
        using HttpResponseMessage response =
            await ownerClient.PutAsJsonAsync(
                endpoint,
                request,
                CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using HttpResponseMessage getResponse =
            await _factory.Client.GetAsync(
                endpoint,
                CancellationToken);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        GetPropertyByIdResponse property =
            Assert.IsType<GetPropertyByIdResponse>(
                await getResponse.Content
                    .ReadFromJsonAsync<GetPropertyByIdResponse>(
                        CancellationToken));

        Assert.Equal(request.Name, property.Name);
        Assert.Equal(request.TimeZoneId, property.TimeZoneId);
        Assert.Equal(request.CheckInTime, property.CheckInTime);
        Assert.Equal(request.CheckOutTime, property.CheckOutTime);
        Assert.True(property.IsActive);
    }

    [Fact]
    public async Task SetStatus_ShouldDeactivateAndReactivate()
    {
        // Arrange
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient ownerClient = _factory.CreateOwnerClient();

        string endpoint =
            $"/api/v1/properties/{seed.PropertyId}/status";

        string detailsEndpoint =
            $"/api/v1/properties/{seed.PropertyId}";

        // Act — Deactivate
        using HttpResponseMessage deactivateResponse =
            await ownerClient.PatchAsJsonAsync(
                endpoint,
                new SetPropertyStatusRequest(false),
                CancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.NoContent,
            deactivateResponse.StatusCode);

        using HttpResponseMessage inactiveResponse =
            await _factory.Client.GetAsync(
                detailsEndpoint,
                CancellationToken);

        GetPropertyByIdResponse inactiveProperty =
            Assert.IsType<GetPropertyByIdResponse>(
                await inactiveResponse.Content
                    .ReadFromJsonAsync<GetPropertyByIdResponse>(
                        CancellationToken));

        Assert.False(inactiveProperty.IsActive);

        // Act — Reactivate
        using HttpResponseMessage activateResponse =
            await ownerClient.PatchAsJsonAsync(
                endpoint,
                new SetPropertyStatusRequest(true),
                CancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.NoContent,
            activateResponse.StatusCode);

        using HttpResponseMessage activeResponse =
            await _factory.Client.GetAsync(
                detailsEndpoint,
                CancellationToken);

        GetPropertyByIdResponse activeProperty =
            Assert.IsType<GetPropertyByIdResponse>(
                await activeResponse.Content
                    .ReadFromJsonAsync<GetPropertyByIdResponse>(
                        CancellationToken));

        Assert.True(activeProperty.IsActive);
    }

    [Fact]
    public async Task SetStatus_ShouldEnforceResourceOwnership()
    {
        // Arrange
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        string endpoint =
            $"/api/v1/properties/{seed.PropertyId}/status";

        var request = new SetPropertyStatusRequest(false);

        using HttpClient otherOwner =
            _factory.CreateOwnerClient("different-owner");

        using HttpClient customer =
            _factory.CreateCustomerClient();

        using HttpClient admin =
            _factory.CreateAdminClient();

        // Anonymous
        using HttpResponseMessage anonymousResponse =
            await _factory.Client.PatchAsJsonAsync(
                endpoint,
                request,
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            anonymousResponse.StatusCode);

        // Wrong Owner
        using HttpResponseMessage wrongOwnerResponse =
            await otherOwner.PatchAsJsonAsync(
                endpoint,
                request,
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            wrongOwnerResponse.StatusCode);

        // Customer
        using HttpResponseMessage customerResponse =
            await customer.PatchAsJsonAsync(
                endpoint,
                request,
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            customerResponse.StatusCode);

        // Admin
        using HttpResponseMessage adminResponse =
            await admin.PatchAsJsonAsync(
                endpoint,
                request,
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.NoContent,
            adminResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateProperty_WithInvalidTimeZone_ShouldPreserveData()
    {
        // Arrange
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient ownerClient = _factory.CreateOwnerClient();

        string endpoint = $"/api/v1/properties/{seed.PropertyId}";

        var request = new UpdatePropertyRequest(
            "Should Not Be Saved",
            "Invalid/Unknown_Zone",
            new TimeOnly(18, 0),
            new TimeOnly(8, 0));

        // Act
        using HttpResponseMessage response =
            await ownerClient.PutAsJsonAsync(
                endpoint,
                request,
                CancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetailsResponse problem =
            Assert.IsType<ProblemDetailsResponse>(
                await response.Content
                    .ReadFromJsonAsync<ProblemDetailsResponse>(
                        CancellationToken));

        Assert.Equal("Property.InvalidTimeZoneId", problem.Code);

        using HttpResponseMessage getResponse =
            await _factory.Client.GetAsync(
                endpoint,
                CancellationToken);

        GetPropertyByIdResponse property =
            Assert.IsType<GetPropertyByIdResponse>(
                await getResponse.Content
                    .ReadFromJsonAsync<GetPropertyByIdResponse>(
                        CancellationToken));

        Assert.Equal(seed.Property.Name, property.Name);
        Assert.Equal(seed.Property.TimeZoneId, property.TimeZoneId);
        Assert.Equal(seed.Property.CheckInTime, property.CheckInTime);
        Assert.Equal(seed.Property.CheckOutTime, property.CheckOutTime);
    }
}
