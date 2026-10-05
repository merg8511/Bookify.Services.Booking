using Bookify.Services.Booking.Api.RateLimiting;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.RateLimiting;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class RateLimitingEndpointMetadataTests
{
    private readonly BookingApiFactory _factory;

    public RateLimitingEndpointMetadataTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(
        "Properties.List",
        BookifyRateLimitPolicies.PublicReads)]
    [InlineData(
        "Properties.GetById",
        BookifyRateLimitPolicies.PublicReads)]
    [InlineData(
        "Properties.GetUnits",
        BookifyRateLimitPolicies.PublicReads)]
    [InlineData(
        "Properties.GetAvailability",
        BookifyRateLimitPolicies.PublicReads)]
    [InlineData(
        "Bookings.Create",
        BookifyRateLimitPolicies.BookingCreation)]
    [InlineData(
        "Payments.Initiate",
        BookifyRateLimitPolicies.PaymentInitiation)]
    [InlineData(
        "Payments.StripeWebhook",
        BookifyRateLimitPolicies.Webhook)]
    public void Endpoint_ShouldUseExpectedRateLimitPolicy(
        string endpointName,
        string expectedPolicy)
    {
        // Arrange
        EndpointDataSource endpointDataSource =
            _factory.Services
                .GetRequiredService<
                    EndpointDataSource>();

        Endpoint endpoint =
            Assert.Single(
                endpointDataSource.Endpoints,
                candidate =>
                    string.Equals(
                        candidate.Metadata
                            .GetMetadata<
                                IEndpointNameMetadata>()?
                            .EndpointName,
                        endpointName,
                        StringComparison.Ordinal));

        // Act
        EnableRateLimitingAttribute? metadata =
            endpoint.Metadata.GetMetadata<
                EnableRateLimitingAttribute>();

        // Assert
        Assert.NotNull(metadata);

        Assert.Equal(
            expectedPolicy,
            metadata.PolicyName);
    }
}
