using Bookify.Services.Booking.Api.Contracts.Pagination;
using Bookify.Services.Booking.Api.Endpoints.Properties.Create;
using Bookify.Services.Booking.Api.Endpoints.Properties.GetAvailability;
using Bookify.Services.Booking.Api.Endpoints.Properties.GetById;
using Bookify.Services.Booking.Api.Endpoints.Properties.GetPaged;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.IntegrationTests.Contracts;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Bookify.Services.Booking.IntegrationTests.Endpoints.Properties;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class PropertyEndpointsTests
{
    private const string PropertiesEndpoint = "/api/v1/properties";
    private readonly HttpClient _client;
    private readonly BookingApiFactory _factory;
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
    public PropertyEndpointsTests(BookingApiFactory factory)
    {
        _client = factory.Client;
        _factory = factory;
    }

    [Fact]
    public async Task CreateProperty_ReturnsCreatedWithLocation()
    {
        CreatedProperty created =
            await CreatePropertyAsync();

        Assert.NotEqual(
            Guid.Empty,
            created.Response.Id);

        Assert.EndsWith(
            $"/api/v1/properties/{created.Response.Id}",
            created.Location.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPropertyById_ReturnsPersistedProperty()
    {
        CreatedProperty created = await CreatePropertyAsync();

        using HttpResponseMessage response = await _client
            .GetAsync(created.Location, CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.Equal(
            "application/json",
            response.Content.Headers
                .ContentType?
                .MediaType);

        GetPropertyByIdResponse body =
            Assert.IsType<GetPropertyByIdResponse>(
                await response.Content
                    .ReadFromJsonAsync<
                        GetPropertyByIdResponse>(CancellationToken));

        Assert.Equal(
            created.Response.Id,
            body.Id);

        Assert.Equal(
            created.Request.Name,
            body.Name);

        Assert.Equal(
            created.Request.TimeZoneId,
            body.TimeZoneId);

        Assert.Equal(
            created.Request.CheckInTime,
            body.CheckInTime);

        Assert.Equal(
            created.Request.CheckOutTime,
            body.CheckOutTime);

        Assert.True(body.IsActive);
    }

    [Fact]
    public async Task CreateProperty_WithInvalidName_ReturnsValidationProblem()
    {
        var request =
            new CreatePropertyRequest(
                "  ",
                "America/El_Salvador",
                new TimeOnly(15, 0),
                new TimeOnly(11, 0));

        using HttpResponseMessage response =
            await _client.PostAsJsonAsync(
            PropertiesEndpoint,
            request,
            CancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetailsResponse problem =
            await ReadProblemAsync(response);

        Assert.Equal(
            "urn:bookify:problem-type:validation",
            problem.Type);

        Assert.Equal(
            "Validation error",
            problem.Title);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            problem.Status);

        Assert.Equal(
            PropertiesEndpoint,
            problem.Instance);

        Assert.Equal(
            "Property.InvalidName",
            problem.Code);

        Assert.False(
            string.IsNullOrWhiteSpace(
                problem.Detail));
    }

    [Fact]
    public async Task GetPropertyById_WhenMissing_ReturnsNotFoundProblem()
    {
        Guid missingPropertyId = Guid.NewGuid();

        string endpoint = $"{PropertiesEndpoint}/" +
            $"{missingPropertyId}";

        using HttpResponseMessage response =
            await _client.GetAsync(endpoint, CancellationToken);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        ProblemDetailsResponse problem =
            await ReadProblemAsync(response);

        Assert.Equal(
            "urn:bookify:problem-type:not-found",
            problem.Type);

        Assert.Equal(
            "Resource not found",
            problem.Title);

        Assert.Equal(
            StatusCodes.Status404NotFound,
            problem.Status);

        Assert.Equal(
            endpoint,
            problem.Instance);

        Assert.Equal(
            "Property.NotFound",
            problem.Code);

        Assert.False(
            string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task GetPropertyById_WithEmptyId_ReturnsValidationProblem()
    {
        string endpoint = $"{PropertiesEndpoint}/" +
            $"{Guid.Empty}";

        using HttpResponseMessage response =
            await _client.GetAsync(
                endpoint,
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetailsResponse problem =
            await ReadProblemAsync(response);

        Assert.Equal(
            "urn:bookify:problem-type:validation",
            problem.Type);

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            problem.Status);

        Assert.Equal(
            endpoint,
            problem.Instance);

        Assert.Equal(
            "Property.InvalidId",
            problem.Code);
    }

    [Fact]
    public async Task CreateProperty_WithUnsupportedContentType_ReturnsUnsupportedMediaType()
    {
        using var content =
            new StringContent(
                "invalid content",
                Encoding.UTF8,
                "text/plain");

        using HttpResponseMessage response =
            await _client.PostAsync(
                PropertiesEndpoint,
                content,
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.UnsupportedMediaType,
            response.StatusCode);
    }

    [Fact]
    public async Task OldUnversionedRoute_ReturnsNotFound()
    {
        using HttpResponseMessage response =
            await _client.GetAsync(
                $"/api/properties/" +
                $"{Guid.NewGuid()}",
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task GetProperties_ReturnsPagedResponse()
    {
        await CreatePropertyAsync();
        await CreatePropertyAsync();
        await CreatePropertyAsync();

        using HttpResponseMessage response =
            await _client.GetAsync(
                $"{PropertiesEndpoint}" +
                $"?pageNumber=1&pageSize=2",
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        PagedResponse<
        PropertyListItemResponse> body =
        Assert.IsType<
            PagedResponse<
                PropertyListItemResponse>>(
            await response.Content
                .ReadFromJsonAsync<
                    PagedResponse<
                        PropertyListItemResponse>>(CancellationToken));

        Assert.Equal(
            1,
            body.PageNumber);

        Assert.Equal(
            2,
            body.PageSize);

        Assert.Equal(
            2,
            body.Items.Count);

        Assert.True(
            body.TotalRecords >= 3);

        Assert.True(
            body.TotalPages >= 2);
    }

    [Fact]
    public async Task GetProperties_WithInvalidPageNumber_ReturnsValidationProblem()
    {
        using HttpResponseMessage response =
            await _client.GetAsync(
                $"{PropertiesEndpoint}" +
                $"?pageNumber=0&pageSize=20",
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetailsResponse problem =
            await ReadProblemAsync(response);

        Assert.Equal(
            "Pagination.InvalidPageNumber",
            problem.Code);
    }

    [Fact]
    public async Task GetProperties_WithPageSizeAboveLimit_ReturnsValidationProblem()
    {
        using HttpResponseMessage response =
            await _client.GetAsync(
                $"{PropertiesEndpoint}" +
                $"?pageNumber=1&pageSize=101",
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetailsResponse problem = await ReadProblemAsync(response);

        Assert.Equal(
            "Pagination.PageSizeExceeded",
            problem.Code);
    }

    [Fact]
    public async Task GetProperties_WithNameFilter_ReturnsMatchingProperties()
    {
        string uniqueToken = Guid.NewGuid().ToString("N");
        string propertyName = $"Rancho {uniqueToken}";

        await CreatePropertyAsync(propertyName);

        string encodedName =
            Uri.EscapeDataString(
                uniqueToken.ToUpperInvariant());

        using HttpResponseMessage response =
            await _client.GetAsync(
                $"{PropertiesEndpoint}" +
                $"?name={encodedName}" +
                $"&isActive=true" +
                $"&pageNumber=1" +
                $"&pageSize=20", CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        PagedResponse<
            PropertyListItemResponse> body =
            Assert.IsType<
                PagedResponse<
                    PropertyListItemResponse>>(
                await response.Content
                    .ReadFromJsonAsync<
                        PagedResponse<
                            PropertyListItemResponse>>(CancellationToken));

        PropertyListItemResponse item =
            Assert.Single(body.Items);

        Assert.Equal(
            propertyName,
            item.Name);

        Assert.True(item.IsActive);

        Assert.Equal(
            1,
            body.TotalRecords);

        Assert.Equal(
            1,
            body.TotalPages);
    }

    [Fact]
    public async Task GetProperties_WithInvalidBooleanFilter_ReturnsBadRequest()
    {
        using HttpResponseMessage response =
            await _client.GetAsync(
                $"{PropertiesEndpoint}" +
                $"?isActive=not-a-boolean",
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task GetProperties_WithDescendingNameSort_ReturnsOrderedItems()
    {
        string token =
            Guid.NewGuid()
                .ToString(
                    "N");

        await CreatePropertyAsync(
            $"{token} Alpha");

        await CreatePropertyAsync(
            $"{token} Charlie");

        await CreatePropertyAsync(
            $"{token} Bravo");

        string encodedToken =
            Uri.EscapeDataString(
                token);

        using HttpResponseMessage response =
            await _client.GetAsync(
                $"{PropertiesEndpoint}" +
                $"?name={encodedToken}" +
                "&sortBy=name" +
                "&sortDirection=desc" +
                "&pageNumber=1" +
                "&pageSize=10",
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        PagedResponse<
            PropertyListItemResponse> body =
            Assert.IsType<
                PagedResponse<
                    PropertyListItemResponse>>(
                await response.Content
                    .ReadFromJsonAsync<
                        PagedResponse<
                            PropertyListItemResponse>>(CancellationToken));

        Assert.Collection(
            body.Items,
            first =>
                Assert.Equal(
                    $"{token} Charlie",
                    first.Name),
            second =>
                Assert.Equal(
                    $"{token} Bravo",
                    second.Name),
            third =>
                Assert.Equal(
                    $"{token} Alpha",
                    third.Name));
    }

    [Fact]
    public async Task GetProperties_WithInvalidSortBy_ReturnsValidationProblem()
    {
        using HttpResponseMessage response =
            await _client.GetAsync(
                $"{PropertiesEndpoint}" +
                "?sortBy=createdOn" +
                "&sortDirection=asc",
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetailsResponse problem =
            await ReadProblemAsync(
                response);

        Assert.Equal(
            "Properties.InvalidSortBy",
            problem.Code);
    }

    [Fact]
    public async Task GetProperties_WithInvalidSortDirection_ReturnsValidationProblem()
    {
        using HttpResponseMessage response =
            await _client.GetAsync(
                $"{PropertiesEndpoint}" +
                "?sortBy=name" +
                "&sortDirection=sideways",
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        ProblemDetailsResponse problem =
            await ReadProblemAsync(
                response);

        Assert.Equal(
            "Sorting.InvalidDirection",
            problem.Code);
    }

    [Fact]
    public async Task GetProperties_WithoutSorting_UsesNameAscending()
    {
        string token =
            Guid.NewGuid()
                .ToString(
                    "N");

        await CreatePropertyAsync(
            $"{token} Charlie");

        await CreatePropertyAsync(
            $"{token} Alpha");

        string encodedToken =
            Uri.EscapeDataString(
                token);

        using HttpResponseMessage response =
            await _client.GetAsync(
                $"{PropertiesEndpoint}" +
                $"?name={encodedToken}" +
                "&pageNumber=1" +
                "&pageSize=10",
                CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        PagedResponse<
            PropertyListItemResponse> body =
            Assert.IsType<
                PagedResponse<
                    PropertyListItemResponse>>(
                await response.Content
                    .ReadFromJsonAsync<
                        PagedResponse<
                            PropertyListItemResponse>>(CancellationToken));

        Assert.Equal(
            $"{token} Alpha",
            body.Items[0].Name);

        Assert.Equal(
            $"{token} Charlie",
            body.Items[1].Name);
    }

    [Fact]
    public async Task
    GetAvailability_WithValidRequest_ReturnsAvailableUnits()
    {
        // Arrange
        AvailabilityTestData data =
            await SeedAvailabilityScenarioAsync(
                TestContext.Current
                    .CancellationToken);

        HttpClient client =
            _factory.CreateClient();

        string url =
            $"/api/v1/properties/" +
            $"{data.PropertyId}/availability" +
            "?checkInDate=2026-08-10" +
            "&checkOutDate=2026-08-15" +
            "&guestCount=2";

        // Act
        HttpResponseMessage response =
            await client.GetAsync(
                url,
                TestContext.Current
                    .CancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        GetAvailabilityResponse? content =
            await response.Content
                .ReadFromJsonAsync<
                    GetAvailabilityResponse>(
                        TestContext.Current
                            .CancellationToken);

        Assert.NotNull(
            content);

        Assert.Equal(
            5,
            content.NumberOfNights);

        AvailableRentableUnitResponse unit =
            Assert.Single(
                content.AvailableUnits);

        Assert.Equal(
            data.RoomBId,
            unit.Id);
    }

    private async Task<CreatedProperty> CreatePropertyAsync(string? name = null)
    {
        var request =
            new CreatePropertyRequest(
                name ?? $"Rancho {Guid.NewGuid():N}",
                "America/El_Salvador",
                new TimeOnly(15, 0),
                new TimeOnly(11, 0));

        using HttpResponseMessage response =
            await _client.PostAsJsonAsync(
                PropertiesEndpoint,
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        Assert.Equal(
            "application/json",
            response.Content.Headers
                .ContentType?
                .MediaType);

        CreatePropertyResponse body =
            Assert.IsType<CreatePropertyResponse>(
                await response.Content
                    .ReadFromJsonAsync<
                        CreatePropertyResponse>());

        Uri location =
            Assert.IsType<Uri>(response.Headers.Location);

        return new CreatedProperty(request, body, location);
    }

    private static async Task<ProblemDetailsResponse>
        ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers
                .ContentType?
                .MediaType);

        return Assert.IsType<ProblemDetailsResponse>(
            await response.Content
                .ReadFromJsonAsync<ProblemDetailsResponse>());
    }

    private sealed record CreatedProperty(
        CreatePropertyRequest Request,
        CreatePropertyResponse Response,
        Uri Location)
    {
    }

    private async Task<AvailabilityTestData> SeedAvailabilityScenarioAsync(
        CancellationToken cancellationToken)
    {
        Guid propertyId = Guid.NewGuid();
        Guid roomAId = Guid.NewGuid();
        Guid roomBId = Guid.NewGuid();
        Guid entirePropertyId = Guid.NewGuid();
        Guid inactiveRoomId = Guid.NewGuid();
        Guid lowCapacityRoomId = Guid.NewGuid();
        Guid roomABookingId = Guid.NewGuid();
        Guid roomBCancelledBookingId = Guid.NewGuid();

        IDbConnectionFactory connectionFactory =
            _factory.Services.GetRequiredService<IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory.OpenConnectionAsync(cancellationToken);

        var command = new CommandDefinition(
            """
            INSERT INTO properties
            (
                id, name, time_zone_id, check_in_time, check_out_time, is_active
            )
            VALUES
            (
                @PropertyId, 'Availability Property', 'America/El_Salvador', '15:00', '11:00', TRUE
            );

            INSERT INTO rentable_units
            (
                id, property_id, name, type, maximum_capacity, max_base_guests, is_active
            )
            VALUES
            (
                @RoomAId, @PropertyId, 'Room A', 'Room', 4, 2, TRUE
            ),
            (
                @RoomBId, @PropertyId, 'Room B', 'Room', 2, 2, TRUE
            ),
            (
                @EntirePropertyId, @PropertyId, 'Entire Property', 'EntireProperty', 10, 6, TRUE
            ),
            (
                @InactiveRoomId, @PropertyId, 'Inactive Room', 'Room', 10, 4, FALSE
            ),
            (
                @LowCapacityRoomId, @PropertyId, 'Small Room', 'Room', 1, 1, TRUE
            );

            INSERT INTO bookings
            (
                id, property_id, rentable_unit_id, check_in_date, check_out_date, guest_count, status, cancellation_reason
            )
            VALUES
            (
                @RoomABookingId, @PropertyId, @RoomAId, '2026-08-10', '2026-08-15', 2, 'Paid', NULL
            ),
            (
                @RoomBCancelledBookingId, @PropertyId, @RoomBId, '2026-08-10', '2026-08-15', 2, 'Cancelled', 'PaymentExpired'
            );
            """,
            new
            {
                PropertyId = propertyId,
                RoomAId = roomAId,
                RoomBId = roomBId,
                EntirePropertyId = entirePropertyId,
                InactiveRoomId = inactiveRoomId,
                LowCapacityRoomId = lowCapacityRoomId,
                RoomABookingId = roomABookingId,
                RoomBCancelledBookingId = roomBCancelledBookingId
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);

        return new AvailabilityTestData(
            propertyId,
            roomAId,
            roomBId,
            entirePropertyId,
            inactiveRoomId,
            lowCapacityRoomId,
            roomABookingId,
            roomBCancelledBookingId);
    }

    private sealed record AvailabilityTestData(
        Guid PropertyId,
        Guid RoomAId,
        Guid RoomBId,
        Guid EntirePropertyId,
        Guid InactiveRoomId,
        Guid LowCapacityRoomId,
        Guid RoomABookingId,
        Guid RoomBCancelledBookingId);
}
