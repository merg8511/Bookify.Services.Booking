using System.Net;
using System.Text.Json;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.OpenApi;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Contract")]
public sealed class OpenApiDocumentContractTests
{
    private const string OpenApiPath = "/openapi/v1.json";
    private readonly BookingApiFactory _factory;

    public OpenApiDocumentContractTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenApi_ShouldBeAvailableInTestingAsVersion31()
    {
        using JsonDocument document = await LoadDocumentAsync();
        JsonElement root = document.RootElement;

        string? version = root.GetProperty("openapi").GetString();

        Assert.True(version is not null && version.StartsWith("3.1.", StringComparison.Ordinal));
        Assert.True(root.GetProperty("paths").TryGetProperty("/health", out _));

        // En Testing no se registran los diagnósticos exclusivos
        // de Development. OpenAPI tampoco debe documentarse a sí mismo.
        Assert.False(root.GetProperty("paths").TryGetProperty("/diagnostics/time", out _));
        Assert.False(root.GetProperty("paths").TryGetProperty("/diagnostics/database", out _));
        Assert.False(root.GetProperty("paths").TryGetProperty(OpenApiPath, out _));
    }

    [Fact]
    public async Task OpenApi_ShouldContainExactly33ExpectedOperationsAndUniqueIds()
    {
        using JsonDocument document = await LoadDocumentAsync();
        JsonElement root = document.RootElement;
        JsonElement paths = root.GetProperty("paths");

        var actualOperations = new Dictionary<string, string>(StringComparer.Ordinal);
        var operationIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (JsonProperty path in paths.EnumerateObject())
        {
            if (!path.Name.StartsWith("/api/v1/", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (string method in HttpMethods)
            {
                if (!path.Value.TryGetProperty(method, out JsonElement operation))
                {
                    continue;
                }

                string? operationId = operation.GetProperty("operationId").GetString();
                Assert.False(string.IsNullOrWhiteSpace(operationId));
                Assert.True(operationIds.Add(operationId!), $"Duplicate operationId: {operationId}");

                string key = $"{method.ToUpperInvariant()} {path.Name}";
                Assert.True(actualOperations.TryAdd(key, operationId!), $"Duplicate operation: {key}");
            }
        }

        Assert.Equal(33, actualOperations.Count);
        Assert.Equal(ExpectedOperations.Length, actualOperations.Count);

        foreach (OperationContract expected in ExpectedOperations)
        {
            string key = $"{expected.Method.ToUpperInvariant()} {expected.Path}";
            Assert.True(actualOperations.TryGetValue(key, out string? actualId),
                $"Missing HTTP contract: {key}");
            Assert.Equal(expected.OperationId, actualId);
        }
    }

    [Fact]
    public async Task OpenApi_ShouldDeclareThreeCorrectSecuritySchemes()
    {
        using JsonDocument document = await LoadDocumentAsync();
        JsonElement schemes = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");

        string[] names = schemes.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            expectedArray,
            names);

        JsonElement bearer = schemes.GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearer.GetProperty("bearerFormat").GetString());

        AssertHeaderSecurityScheme(
            schemes.GetProperty("BookingGuestToken"),
            "Booking-Guest-Token");

        AssertHeaderSecurityScheme(
            schemes.GetProperty("StripeSignature"),
            "Stripe-Signature");
    }

    [Fact]
    public async Task OpenApi_ShouldApplyCorrectSecurityAlternativesToEveryOperation()
    {
        using JsonDocument document = await LoadDocumentAsync();
        JsonElement root = document.RootElement;

        // No hay seguridad global: cada endpoint declara su propia política.
        Assert.False(root.TryGetProperty("security", out _));

        foreach (OperationContract expected in ExpectedOperations)
        {
            JsonElement operation = FindOperation(root, expected);
            string[] actual = GetSecurityAlternatives(operation)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            string[] required = expected.Access switch
            {
                AccessModel.Public => [],
                AccessModel.Bearer => ["Bearer"],
                AccessModel.CustomerOrGuest => ["Bearer", "BookingGuestToken"],
                AccessModel.StripeWebhook => ["StripeSignature"],
                _ => throw new InvalidOperationException("Unknown security category.")
            };

            Array.Sort(required, StringComparer.Ordinal);
            Assert.Equal(required, actual);
        }
    }

    [Fact]
    public async Task OpenApi_ShouldRequireIdempotencyOnlyForBookingCreationAndPaymentInitiation()
    {
        using JsonDocument document = await LoadDocumentAsync();
        JsonElement root = document.RootElement;

        string[] idempotentOperations = ["Bookings.Create", "Payments.Initiate"];

        foreach (OperationContract expected in ExpectedOperations)
        {
            JsonElement operation = FindOperation(root, expected);
            JsonElement[] headers = FindHeaderParameters(operation, "Idempotency-Key");

            if (idempotentOperations.Contains(expected.OperationId, StringComparer.Ordinal))
            {
                JsonElement header = Assert.Single(headers);
                Assert.True(IsRequired(header));
                JsonElement schema = header.GetProperty("schema");
                Assert.Equal("string", schema.GetProperty("type").GetString());
                Assert.Equal(1, schema.GetProperty("minLength").GetInt32());
                Assert.Equal(255, schema.GetProperty("maxLength").GetInt32());
            }
            else
            {
                Assert.Empty(headers);
            }
        }
    }

    [Fact]
    public async Task BookingCreation_ShouldDocumentOptionalOneTimeGuestCredentialResponseHeader()
    {
        using JsonDocument document = await LoadDocumentAsync();
        JsonElement operation = FindOperation(document.RootElement, "Bookings.Create");
        JsonElement created = operation.GetProperty("responses").GetProperty("201");

        JsonElement credential = created.GetProperty("headers")
            .GetProperty("Booking-Guest-Token");

        Assert.False(IsRequired(credential));
        Assert.Equal("string", credential.GetProperty("schema").GetProperty("type").GetString());
        Assert.Contains("not reissued", credential.GetProperty("description").GetString()!,
            StringComparison.OrdinalIgnoreCase);
        Assert.Empty(GetSecurityAlternatives(operation));
    }

    [Fact]
    public async Task StripeWebhook_ShouldDocumentRawJsonBodyAndSignatureOnly()
    {
        using JsonDocument document = await LoadDocumentAsync();
        JsonElement operation = FindOperation(document.RootElement, "Payments.StripeWebhook");

        Assert.Equal(expected, GetSecurityAlternatives(operation));
        Assert.True(operation.GetProperty("requestBody")
            .GetProperty("content")
            .TryGetProperty("application/json", out _));
        Assert.True(operation.GetProperty("responses").TryGetProperty("200", out _));
    }

    [Fact]
    public async Task RateLimitedOperations_ShouldDocument429ProblemAndOptionalRetryAfter()
    {
        using JsonDocument document = await LoadDocumentAsync();
        JsonElement root = document.RootElement;

        string[] rateLimitedOperations =
        [
            "Properties.List",
            "Properties.GetUnits",
            "Properties.GetAvailability",
            "Bookings.Create",
            "Payments.Initiate",
            "Payments.StripeWebhook"
        ];

        foreach (string operationId in rateLimitedOperations)
        {
            JsonElement operation = FindOperation(root, operationId);
            JsonElement tooManyRequests = operation.GetProperty("responses").GetProperty("429");

            Assert.True(tooManyRequests.GetProperty("content")
                .TryGetProperty("application/problem+json", out _));

            JsonElement retryAfter = tooManyRequests.GetProperty("headers")
                .GetProperty("Retry-After");

            Assert.False(IsRequired(retryAfter));
            Assert.Equal("string", retryAfter.GetProperty("schema").GetProperty("type").GetString());
            Assert.Contains("RateLimit.Exceeded", tooManyRequests.GetProperty("description").GetString()!, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ConflictAndValidationResponses_ShouldUseProblemDetails()
    {
        using JsonDocument document = await LoadDocumentAsync();
        JsonElement root = document.RootElement;

        AssertProblemResponse(FindOperation(root, "Bookings.Create"), "400");
        AssertProblemResponse(FindOperation(root, "Bookings.Create"), "409");
        AssertProblemResponse(FindOperation(root, "Bookings.Create"), "500");
        AssertProblemResponse(FindOperation(root, "Payments.Initiate"), "409");
        AssertProblemResponse(FindOperation(root, "Payments.Initiate"), "504");
        AssertProblemResponse(FindOperation(root, "Properties.GetAvailability"), "409");

        JsonElement admin = FindOperation(root, "AdminBookings.List");
        Assert.True(admin.GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(admin.GetProperty("responses").TryGetProperty("403", out _));
    }

    [Fact]
    public async Task PagedOperations_ShouldDocumentDefaultsAndBounds()
    {
        using JsonDocument document = await LoadDocumentAsync();
        JsonElement root = document.RootElement;

        foreach (string operationId in new[] { "Properties.List", "AdminBookings.List" })
        {
            JsonElement operation = FindOperation(root, operationId);

            JsonElement pageNumber = Assert.Single(FindQueryParameters(operation, "pageNumber"));
            JsonElement numberSchema = pageNumber.GetProperty("schema");
            Assert.Equal(1, numberSchema.GetProperty("minimum").GetInt32());
            Assert.Equal(1, numberSchema.GetProperty("default").GetInt32());

            JsonElement pageSize = Assert.Single(FindQueryParameters(operation, "pageSize"));
            JsonElement sizeSchema = pageSize.GetProperty("schema");
            Assert.Equal(1, sizeSchema.GetProperty("minimum").GetInt32());
            Assert.Equal(100, sizeSchema.GetProperty("maximum").GetInt32());
            Assert.Equal(20, sizeSchema.GetProperty("default").GetInt32());
        }
    }

    [Fact]
    public async Task Document_ShouldNotContainConfiguredWebhookSecret()
    {
        using JsonDocument document = await LoadDocumentAsync();
        string json = document.RootElement.GetRawText();

        Assert.DoesNotContain(BookingApiFactory.StripeWebhookSecret, json, StringComparison.Ordinal);
        Assert.DoesNotContain("whsec_bookify_integration_tests", json, StringComparison.Ordinal);
    }

    private async Task<JsonDocument> LoadDocumentAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using HttpResponseMessage response = await _factory.Client.GetAsync(
            OpenApiPath,
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static JsonElement FindOperation(JsonElement root, OperationContract expected)
    {
        return root.GetProperty("paths")
            .GetProperty(expected.Path)
            .GetProperty(expected.Method);
    }

    private static JsonElement FindOperation(JsonElement root, string operationId)
    {
        OperationContract contract = Assert.Single(ExpectedOperations, item => item.OperationId == operationId);

        return FindOperation(root, contract);
    }

    private static string[] GetSecurityAlternatives(JsonElement operation)
    {
        if (!operation.TryGetProperty("security", out JsonElement security))
        {
            return [];
        }

        Assert.Equal(JsonValueKind.Array, security.ValueKind);
        var alternatives = new List<string>();

        foreach (JsonElement requirement in security.EnumerateArray())
        {
            // Cada entrada debe contener UN mecanismo. Dos entradas
            // representan OR; una entrada con dos mecanismos sería AND.
            JsonProperty item = Assert.Single(requirement.EnumerateObject());
            Assert.Equal(JsonValueKind.Array, item.Value.ValueKind);
            Assert.Equal(0, item.Value.GetArrayLength());
            alternatives.Add(item.Name);
        }

        return alternatives.ToArray();
    }

    private static JsonElement[] FindHeaderParameters(JsonElement operation, string headerName)
    {
        return FindParameters(operation, "header", headerName);
    }

    private static JsonElement[] FindQueryParameters(JsonElement operation, string parameterName)
    {
        return FindParameters(operation, "query", parameterName);
    }

    private static JsonElement[] FindParameters(JsonElement operation, string location, string name)
    {
        if (!operation.TryGetProperty("parameters", out JsonElement parameters))
        {
            return [];
        }

        return parameters.EnumerateArray()
            .Where(parameter =>
                parameter.TryGetProperty("in", out JsonElement locationNode) &&
                locationNode.GetString() == location &&
                parameter.TryGetProperty("name", out JsonElement nameNode) &&
                string.Equals(nameNode.GetString(), name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static bool IsRequired(JsonElement element)
    {
        return element.TryGetProperty("required", out JsonElement required) &&
            required.ValueKind == JsonValueKind.True;
    }

    private static void AssertProblemResponse(JsonElement operation, string statusCode)
    {
        JsonElement response = operation.GetProperty("responses").GetProperty(statusCode);
        Assert.True(response.GetProperty("content")
            .TryGetProperty("application/problem+json", out _));
    }

    private static void AssertHeaderSecurityScheme(JsonElement scheme, string headerName)
    {
        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("header", scheme.GetProperty("in").GetString());
        Assert.Equal(headerName, scheme.GetProperty("name").GetString());
    }

    private static readonly string[] HttpMethods = ["get", "post", "put", "patch", "delete"];

    private enum AccessModel
    {
        Public,
        Bearer,
        CustomerOrGuest,
        StripeWebhook
    }

    private sealed record OperationContract(
        string Method,
        string Path,
        string OperationId,
        AccessModel Access);

    private static readonly OperationContract[] ExpectedOperations =
    [
        // Administrative bookings: seis operaciones protegidas por Bearer.
        new("get", "/api/v1/admin/properties/{propertyId}/bookings", "AdminBookings.List", AccessModel.Bearer),
        new("get", "/api/v1/admin/properties/{propertyId}/bookings/{bookingId}", "AdminBookings.GetById", AccessModel.Bearer),
        new("post", "/api/v1/admin/properties/{propertyId}/bookings/{bookingId}/approve", "AdminBookings.Approve", AccessModel.Bearer),
        new("get", "/api/v1/admin/properties/{propertyId}/bookings/{bookingId}/payment", "AdminBookings.GetPayment", AccessModel.Bearer),
        new("post", "/api/v1/admin/properties/{propertyId}/bookings/{bookingId}/reject", "AdminBookings.Reject", AccessModel.Bearer),
        new("get", "/api/v1/admin/properties/{propertyId}/calendar", "AdminBookings.GetCalendar", AccessModel.Bearer),

        // Rentable units, pricing y temporadas administrativas.
        new("get", "/api/v1/admin/properties/{propertyId}/units", "AdminRentableUnits.List", AccessModel.Bearer),
        new("post", "/api/v1/admin/properties/{propertyId}/units", "AdminRentableUnits.Create", AccessModel.Bearer),
        new("get", "/api/v1/admin/properties/{propertyId}/units/{unitId}", "AdminRentableUnits.GetById", AccessModel.Bearer),
        new("put", "/api/v1/admin/properties/{propertyId}/units/{unitId}", "AdminRentableUnits.Update", AccessModel.Bearer),
        new("get", "/api/v1/admin/properties/{propertyId}/units/{unitId}/pricing", "AdminRentableUnits.GetPricing", AccessModel.Bearer),
        new("put", "/api/v1/admin/properties/{propertyId}/units/{unitId}/pricing", "AdminRentableUnits.ConfigurePricing", AccessModel.Bearer),
        new("get", "/api/v1/admin/properties/{propertyId}/units/{unitId}/seasons", "AdminRentableUnits.ListSeason", AccessModel.Bearer),
        new("post", "/api/v1/admin/properties/{propertyId}/units/{unitId}/seasons", "AdminRentableUnits.CreateSeason", AccessModel.Bearer),
        new("get", "/api/v1/admin/properties/{propertyId}/units/{unitId}/seasons/{seasonId}", "AdminRentableUnits.GetSeasonById", AccessModel.Bearer),
        new("put", "/api/v1/admin/properties/{propertyId}/units/{unitId}/seasons/{seasonId}", "AdminRentableUnits.UpdateSeason", AccessModel.Bearer),
        new("patch", "/api/v1/admin/properties/{propertyId}/units/{unitId}/seasons/{seasonId}/status", "AdminRentableUnits.SetSeasonStatus", AccessModel.Bearer),
        new("patch", "/api/v1/admin/properties/{propertyId}/units/{unitId}/status", "AdminRentableUnits.SetStatus", AccessModel.Bearer),

        // Booking: creación pública, acceso Guest o Bearer y acciones Owner.
        new("post", "/api/v1/bookings", "Bookings.Create", AccessModel.Public),
        new("post", "/api/v1/bookings/{bookingId}/approve", "Bookings.Approve", AccessModel.Bearer),
        new("post", "/api/v1/bookings/{bookingId}/cancel", "Bookings.Cancel", AccessModel.CustomerOrGuest),
        new("post", "/api/v1/bookings/{bookingId}/reject", "Bookings.Reject", AccessModel.Bearer),
        new("get", "/api/v1/bookings/{identifier}", "Bookings.GetByIdentifier", AccessModel.CustomerOrGuest),

        // Payments: Bearer o Guest; webhook firmado por Stripe.
        new("post", "/api/v1/payments", "Payments.Initiate", AccessModel.CustomerOrGuest),
        new("get", "/api/v1/payments/bookings/{bookingId}/status", "Payments.GetStatus", AccessModel.CustomerOrGuest),
        new("post", "/api/v1/payments/webhooks/stripe", "Payments.StripeWebhook", AccessModel.StripeWebhook),

        // Propiedades: lecturas públicas y escrituras Owner / Admin.
        new("get", "/api/v1/properties", "Properties.List", AccessModel.Public),
        new("post", "/api/v1/properties", "Properties.Create", AccessModel.Bearer),
        new("get", "/api/v1/properties/{propertyId}", "Properties.GetById", AccessModel.Public),
        new("put", "/api/v1/properties/{propertyId}", "Properties.Update", AccessModel.Bearer),
        new("get", "/api/v1/properties/{propertyId}/availability", "Properties.GetAvailability", AccessModel.Public),
        new("patch", "/api/v1/properties/{propertyId}/status", "Properties.SetStatus", AccessModel.Bearer),
        new("get", "/api/v1/properties/{propertyId}/units", "Properties.GetUnits", AccessModel.Public)
    ];
    private static readonly string[] expected = new[] { "StripeSignature" };
    private static readonly string[] expectedArray = new[] { "Bearer", "BookingGuestToken", "StripeSignature" };
}
