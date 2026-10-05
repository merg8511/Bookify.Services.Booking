using Bookify.Services.Booking.Api.Security.Authorization;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.Security;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class AuthorizationPolicyRegistrationTests
{
    private readonly BookingApiFactory _factory;

    public AuthorizationPolicyRegistrationTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(BookifyAuthorizationPolicies.AdminOnly)]
    [InlineData(BookifyAuthorizationPolicies.PropertyOwnerOrAdmin)]
    [InlineData(BookifyAuthorizationPolicies.BookingOwner)]
    [InlineData(BookifyAuthorizationPolicies.BookingGuestAccess)]
    [InlineData(BookifyAuthorizationPolicies.CustomerOrGuestBookingAccess)]
    public async Task Policy_ShouldBeRegistered(string policyName)
    {
        // Arrange
        IAuthorizationPolicyProvider provider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        AuthorizationPolicy? policy = await provider.GetPolicyAsync(policyName);

        // Assert
        Assert.NotNull(policy);
    }
}
