using System.Net;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Security;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class OwnerAdminEndpointAccessTests
{
    private readonly BookingApiFactory _factory;

    public OwnerAdminEndpointAccessTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task BookingOwnerAction_WithoutJwt_ShouldReturnUnauthorized(string action)
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();

        string endpoint =
            $"/api/v1/bookings/" +
            $"{Guid.NewGuid():D}/" +
            $"{action}";

        // Act
        using HttpResponseMessage response = await client.PostAsync(endpoint, content: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task BookingOwnerAction_WithCustomerRole_ShouldReturnForbidden(string action)
    {
        // Arrange
        using HttpClient client = _factory.CreateCustomerClient();

        string endpoint =
            $"/api/v1/bookings/" +
            $"{Guid.NewGuid():D}/" +
            $"{action}";

        // Act
        using HttpResponseMessage response = await client.PostAsync(endpoint, content: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
