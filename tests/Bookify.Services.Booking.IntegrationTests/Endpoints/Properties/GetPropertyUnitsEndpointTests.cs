using Bookify.Services.Booking.Api.Endpoints.Properties.GetUnits;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.IntegrationTests.Contracts;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Bookify.Services.Booking.IntegrationTests.Endpoints.Properties;

[Collection(
    BookingApiTestFixture.Name)]
[Trait(
    "Category",
    "Integration")]
public sealed class GetPropertyUnitsEndpointTests
{
    private readonly BookingApiFactory _factory;

    public GetPropertyUnitsEndpointTests(
        BookingApiFactory factory)
    {
        _factory =
            factory;
    }

    [Fact]
    public async Task
        Get_WhenPropertyIsActive_ShouldReturnOnlyActiveUnits()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        SeedData data =
            await SeedAsync(
                propertyActive: true,
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        // Act
        using HttpResponseMessage response =
            await client.GetAsync(
                $"/api/v1/properties/" +
                $"{data.PropertyId}/units",
                cancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        GetPropertyUnitsResponse body =
            Assert.IsType<
                GetPropertyUnitsResponse>(
                    await response.Content
                        .ReadFromJsonAsync<
                            GetPropertyUnitsResponse>(
                                cancellationToken));

        Assert.Equal(
            data.PropertyId,
            body.PropertyId);

        Assert.Equal(
            2,
            body.Units.Count);

        PropertyRentableUnitResponse
            entireProperty =
                Assert.Single(
                    body.Units,
                    unit =>
                        unit.Id ==
                        data.EntirePropertyId);

        Assert.Equal(
            "Entire Property",
            entireProperty.Name);

        Assert.Equal(
            "EntireProperty",
            entireProperty.Type);

        Assert.Equal(
            10,
            entireProperty.MaximumCapacity);

        Assert.Equal(
            6,
            entireProperty.MaxBaseGuests);

        Assert.True(
            entireProperty.IsEntireProperty);

        PropertyRentableUnitResponse room =
            Assert.Single(
                body.Units,
                unit =>
                    unit.Id ==
                    data.RoomId);

        Assert.Equal(
            "Room A",
            room.Name);

        Assert.Equal(
            "Room",
            room.Type);

        Assert.Equal(
            4,
            room.MaximumCapacity);

        Assert.Equal(
            2,
            room.MaxBaseGuests);

        Assert.False(
            room.IsEntireProperty);

        Assert.DoesNotContain(
            body.Units,
            unit =>
                unit.Id ==
                data.InactiveRoomId);
    }

    [Fact]
    public async Task
        Get_WhenPropertyDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        Guid propertyId =
            Guid.NewGuid();

        HttpClient client =
            _factory.CreateClient();

        // Act
        using HttpResponseMessage response =
            await client.GetAsync(
                $"/api/v1/properties/" +
                $"{propertyId}/units",
                TestContext.Current
                    .CancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        ProblemDetailsResponse problem =
            Assert.IsType<
                ProblemDetailsResponse>(
                    await response.Content
                        .ReadFromJsonAsync<
                            ProblemDetailsResponse>(
                                TestContext.Current
                                    .CancellationToken));

        Assert.Equal(
            "Property.NotFound",
            problem.Code);
    }

    [Fact]
    public async Task
        Get_WhenPropertyIsInactive_ShouldReturnConflict()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        SeedData data =
            await SeedAsync(
                propertyActive: false,
                cancellationToken);

        HttpClient client =
            _factory.CreateClient();

        // Act
        using HttpResponseMessage response =
            await client.GetAsync(
                $"/api/v1/properties/" +
                $"{data.PropertyId}/units",
                cancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        ProblemDetailsResponse problem =
            Assert.IsType<
                ProblemDetailsResponse>(
                    await response.Content
                        .ReadFromJsonAsync<
                            ProblemDetailsResponse>(
                                cancellationToken));

        Assert.Equal(
            "Property.Inactive",
            problem.Code);
    }

    [Fact]
    public async Task
        Get_WithEmptyPropertyId_ShouldReturnBadRequest()
    {
        HttpClient client =
            _factory.CreateClient();

        using HttpResponseMessage response =
            await client.GetAsync(
                $"/api/v1/properties/" +
                $"{Guid.Empty}/units",
                TestContext.Current
                    .CancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetailsResponse problem =
            Assert.IsType<
                ProblemDetailsResponse>(
                    await response.Content
                        .ReadFromJsonAsync<
                            ProblemDetailsResponse>(
                                TestContext.Current
                                    .CancellationToken));

        Assert.Equal(
            "Property.InvalidId",
            problem.Code);
    }

    private async Task<SeedData> SeedAsync(
        bool propertyActive,
        CancellationToken cancellationToken)
    {
        Property property =
            Property.Create(
                    $"Public Property " +
                    $"{Guid.NewGuid():N}",
                    "America/El_Salvador",
                    new TimeOnly(
                        15,
                        0),
                    new TimeOnly(
                        11,
                        0))
                .Value;

        if (!propertyActive)
        {
            property.Deactivate();
        }

        RentableUnit room =
            RentableUnit.Create(
                    property.Id,
                    "Room A",
                    RentableUnitType.Room,
                    maximumCapacity: 4,
                    maxBaseGuests: 2)
                .Value;

        RentableUnit entireProperty =
            RentableUnit.Create(
                    property.Id,
                    "Entire Property",
                    RentableUnitType.EntireProperty,
                    maximumCapacity: 10,
                    maxBaseGuests: 6)
                .Value;

        RentableUnit inactiveRoom =
            RentableUnit.Create(
                    property.Id,
                    "Inactive Room",
                    RentableUnitType.Room,
                    maximumCapacity: 2,
                    maxBaseGuests: 2)
                .Value;

        inactiveRoom.Deactivate();

        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IPropertyRepository propertyRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IPropertyRepository>();

        IRentableUnitRepository
            rentableUnitRepository =
                scope.ServiceProvider
                    .GetRequiredService<
                        IRentableUnitRepository>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        propertyRepository.Add(
            property);

        rentableUnitRepository.Add(
            room);

        rentableUnitRepository.Add(
            entireProperty);

        rentableUnitRepository.Add(
            inactiveRoom);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return new SeedData(
            property.Id,
            room.Id,
            entireProperty.Id,
            inactiveRoom.Id);
    }

    private sealed record SeedData(
        Guid PropertyId,
        Guid RoomId,
        Guid EntirePropertyId,
        Guid InactiveRoomId);
}
