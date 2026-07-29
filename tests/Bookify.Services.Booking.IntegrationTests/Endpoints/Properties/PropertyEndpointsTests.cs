using Bookify.Services.Booking.Api.Contracts.Pagination;
using Bookify.Services.Booking.Api.Endpoints.Properties.Create;
using Bookify.Services.Booking.Api.Endpoints.Properties.GetById;
using Bookify.Services.Booking.Api.Endpoints.Properties.GetPaged;
using Bookify.Services.Booking.IntegrationTests.Contracts;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;
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
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
    public PropertyEndpointsTests(BookingApiFactory factory)
    {
        _client = factory.Client;
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
}
