using System.Net;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class SystemBookingEndpointExposureTests
{
    private readonly BookingApiFactory _factory;

    public SystemBookingEndpointExposureTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("mark-as-paid")]
    [InlineData("expire-payment")]
    [InlineData("complete")]
    public async Task Post_SystemOnlyOperation_ShouldNotBeExposedThroughHttp(
        string action)
    {
        // Arrange
        string endpoint =
            $"/api/v1/bookings/" +
            $"{Guid.Empty:D}/" +
            $"{action}";

        // Act
        using HttpResponseMessage response =
            await _factory.Client.PostAsync(
                endpoint,
                content: null,
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
}
