using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Endpoints.Properties.Create;
using Bookify.Services.Booking.Application.Abstractions.Security;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Properties;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class PropertyOwnershipEndpointTests
{
    private readonly BookingApiFactory _factory;

    public PropertyOwnershipEndpointTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateProperty_WithoutJwt_ShouldReturnUnauthorized()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/properties",
            CreateRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateProperty_WithCustomerRole_ShouldReturnForbidden()
    {
        using HttpClient client = _factory.CreateAuthenticatedClient(
            "customer-123",
            BookifyRoles.Customer);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/properties",
            CreateRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(BookifyRoles.Owner)]
    [InlineData(BookifyRoles.Admin)]
    public async Task CreateProperty_WithAllowedRole_ShouldPersistJwtSubject(string role)
    {
        string subject = $"owner-{Guid.NewGuid():N}";

        using HttpClient client = _factory.CreateAuthenticatedClient(subject, role);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/properties",
            CreateRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        CreatePropertyResponse body = Assert.IsType<CreatePropertyResponse>(
            await response.Content.ReadFromJsonAsync<CreatePropertyResponse>(
                TestContext.Current.CancellationToken));

        using IServiceScope scope = _factory.Services.CreateScope();
        BookingDbContext dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        Property property = await dbContext.Properties.AsNoTracking().SingleAsync(
            p => p.Id == body.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(subject, property.OwnerSubjectId);
    }

    [Fact]
    public async Task CreateProperty_WithTamperedJwt_ShouldReturnUnauthorized()
    {
        using HttpClient client = _factory.CreateClient();
        string validToken = TestIdentityTokens.Create("owner-123", BookifyRoles.Owner);
        string tamperedToken = validToken + "tampered";

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            JwtBearerDefaults.AuthenticationScheme,
            tamperedToken);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/properties",
            CreateRequest(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateProperty_WithGuestCredential_ShouldReturnUnauthorized()
    {
        // Arrange
        using HttpClient client =
            _factory.CreateGuestClient(
                new string('x', 43));

        // Act
        using HttpResponseMessage response =
            await client.PostAsJsonAsync(
                "/api/v1/properties",
                CreateRequest(),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    private static CreatePropertyRequest CreateRequest()
    {
        return BookingRequestTestFactory.CreatePropertyRequest($"Ownership Test {Guid.NewGuid():N}");
    }
}
