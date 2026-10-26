
using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Administrative.RentableUnits;
using Bookify.Services.Booking.Integration.Tests.Contracts;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Properties;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class AdminRentableUnitsEndpointSmokeTests
{
    private readonly BookingApiFactory _factory;

    private static CancellationToken CancellationToken =>
        TestContext.Current.CancellationToken;

    public AdminRentableUnitsEndpointSmokeTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    private static string UnitsUrl(Guid propertyId) =>
        $"/api/v1/admin/properties/{propertyId}/units";

    private static string UnitUrl(Guid propertyId, Guid unitId) =>
        $"{UnitsUrl(propertyId)}/{unitId}";

    [Fact]
    public async Task CreateAndGet_ShouldPersistNewUnit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // Arrange
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        var request = new CreateRentableUnitRequest(
            "New Suite",
            "Room",
            4,
            2);

        // Act
        using HttpResponseMessage createResponse =
            await owner.PostAsJsonAsync(
                UnitsUrl(seed.PropertyId),
                request,
                CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        CreateRentableUnitResponse created =
            Assert.IsType<CreateRentableUnitResponse>(
                await createResponse.Content
                    .ReadFromJsonAsync<CreateRentableUnitResponse>(
                        CancellationToken));

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.NotNull(createResponse.Headers.Location);
        Assert.EndsWith(
            UnitUrl(seed.PropertyId, created.Id),
            createResponse.Headers.Location.ToString(),
            StringComparison.Ordinal);

        using HttpResponseMessage getResponse =
            await owner.GetAsync(
                UnitUrl(seed.PropertyId, created.Id),
                CancellationToken);

        var responseBody = await getResponse.Content.ReadAsStringAsync(cancellationToken);

        Assert.True(
            getResponse.StatusCode == HttpStatusCode.OK,
            $"Expected 200 OK, received {(int)getResponse.StatusCode}. " +
            $"Response: {responseBody}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        AdminRentableUnitResponse unit =
            Assert.IsType<AdminRentableUnitResponse>(
                await getResponse.Content
                    .ReadFromJsonAsync<AdminRentableUnitResponse>(
                        CancellationToken));

        Assert.Equal(created.Id, unit.Id);
        Assert.Equal(seed.PropertyId, unit.PropertyId);
        Assert.Equal(request.Name, unit.Name);
        Assert.Equal(request.Type, unit.Type);
        Assert.Equal(request.MaximumCapacity, unit.MaximumCapacity);
        Assert.Equal(request.MaxBaseGuests, unit.MaxBaseGuests);
        Assert.True(unit.IsActive);
        Assert.False(unit.IsEntireProperty);
    }

    [Fact]
    public async Task Update_UnbookedUnit_ShouldPersistTypeAndCapacity()
    {
        // Arrange
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        var request = new UpdateRentableUnitRequest(
            "Full Property",
            "EntireProperty",
            12,
            6);

        // Act
        using HttpResponseMessage response =
            await owner.PutAsJsonAsync(
                UnitUrl(seed.PropertyId, seed.RentableUnitId),
                request,
                CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using HttpResponseMessage getResponse =
            await owner.GetAsync(
                UnitUrl(seed.PropertyId, seed.RentableUnitId),
                CancellationToken);

        AdminRentableUnitResponse unit =
            Assert.IsType<AdminRentableUnitResponse>(
                await getResponse.Content
                    .ReadFromJsonAsync<AdminRentableUnitResponse>(
                        CancellationToken));

        Assert.Equal(request.Name, unit.Name);
        Assert.Equal("EntireProperty", unit.Type);
        Assert.Equal(12, unit.MaximumCapacity);
        Assert.Equal(6, unit.MaxBaseGuests);
        Assert.True(unit.IsEntireProperty);
    }

    [Fact]
    public async Task SetStatus_ShouldBeIdempotentAndVisibleInAdminList()
    {
        // Arrange
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        string statusUrl =
            $"{UnitUrl(seed.PropertyId, seed.RentableUnitId)}/status";

        var request = new SetRentableUnitStatusRequest(false);

        // Act — Deactivate twice.
        using HttpResponseMessage first =
            await owner.PatchAsJsonAsync(
                statusUrl,
                request,
                CancellationToken);

        using HttpResponseMessage second =
            await owner.PatchAsJsonAsync(
                statusUrl,
                request,
                CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        using HttpResponseMessage listResponse =
            await owner.GetAsync(
                UnitsUrl(seed.PropertyId),
                CancellationToken);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        GetAdminRentableUnitsResponse list =
            Assert.IsType<GetAdminRentableUnitsResponse>(
                await listResponse.Content
                    .ReadFromJsonAsync<GetAdminRentableUnitsResponse>(
                        CancellationToken));

        AdminRentableUnitResponse unit = Assert.Single(
            list.Units,
            item => item.Id == seed.RentableUnitId);

        Assert.False(unit.IsActive);

        // The public endpoint still hides inactive units.
        using HttpResponseMessage publicResponse =
            await _factory.Client.GetAsync(
                $"/api/v1/properties/{seed.PropertyId}/units",
                CancellationToken);

        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);

        string publicJson =
            await publicResponse.Content.ReadAsStringAsync(
                CancellationToken);

        Assert.DoesNotContain(
            seed.RentableUnitId.ToString(),
            publicJson,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Management_ShouldEnforcePropertyOwnership()
    {
        // Arrange
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        string endpoint = UnitsUrl(seed.PropertyId);

        using HttpClient otherOwner =
            _factory.CreateOwnerClient("another-owner");

        using HttpClient customer =
            _factory.CreateCustomerClient();

        using HttpClient admin =
            _factory.CreateAdminClient();

        // Anonymous
        using HttpResponseMessage anonymousResponse =
            await _factory.Client.GetAsync(
                endpoint,
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            anonymousResponse.StatusCode);

        // Wrong owner
        using HttpResponseMessage otherOwnerResponse =
            await otherOwner.GetAsync(
                endpoint,
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            otherOwnerResponse.StatusCode);

        // Customer
        using HttpResponseMessage customerResponse =
            await customer.GetAsync(
                endpoint,
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            customerResponse.StatusCode);

        // Admin
        using HttpResponseMessage adminResponse =
            await admin.GetAsync(
                endpoint,
                CancellationToken);

        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
    }

    [Fact]
    public async Task Update_BookedUnit_ShouldRejectTypeChange()
    {
        // Arrange
        var booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory,
            cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        var request = new UpdateRentableUnitRequest(
            "Changed Type",
            "EntireProperty",
            10,
            5);

        // Act
        using HttpResponseMessage response =
            await owner.PutAsJsonAsync(
                UnitUrl(booking.PropertyId, booking.RentableUnitId),
                request,
                CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        ProblemDetailsResponse problem =
            Assert.IsType<ProblemDetailsResponse>(
                await response.Content
                    .ReadFromJsonAsync<ProblemDetailsResponse>(
                        CancellationToken));

        Assert.Equal(
            "RentableUnit.TypeChangeHasBookings",
            problem.Code);
    }

    [Fact]
    public async Task Update_BookedUnit_ShouldRejectInsufficientCapacity()
    {
        // Arrange
        var booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory,
            cancellationToken: CancellationToken);

        using HttpClient owner = _factory.CreateOwnerClient();

        var request = new UpdateRentableUnitRequest(
            "Reduced Capacity",
            "Room",
            MaximumCapacity: 1,
            MaxBaseGuests: 1);

        // Act
        using HttpResponseMessage response =
            await owner.PutAsJsonAsync(
                UnitUrl(booking.PropertyId, booking.RentableUnitId),
                request,
                CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        ProblemDetailsResponse problem =
            Assert.IsType<ProblemDetailsResponse>(
                await response.Content
                    .ReadFromJsonAsync<ProblemDetailsResponse>(
                        CancellationToken));

        Assert.Equal(
            "RentableUnit.CapacityBelowBookedGuestCount",
            problem.Code);
    }

    [Fact]
    public async Task GetUnit_FromDifferentProperty_ShouldReturnNotFound()
    {
        // Arrange
        SeedData first =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        SeedData second =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        using HttpClient admin = _factory.CreateAdminClient();

        // Act
        using HttpResponseMessage response =
            await admin.GetAsync(
                UnitUrl(second.PropertyId, first.RentableUnitId),
                CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
