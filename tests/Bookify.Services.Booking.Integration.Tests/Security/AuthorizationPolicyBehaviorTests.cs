using System.Security.Claims;
using Bookify.Services.Booking.Api.Security.Authorization;
using Bookify.Services.Booking.Application.Abstractions.Security;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Security;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Security")]
public sealed class AuthorizationPolicyBehaviorTests
{
    private const string GuestTokenHeader =
        "Booking-Guest-Token";

    private const string CancelEndpointName =
        "Bookings.Cancel";

    private readonly BookingApiFactory _factory;

    public AuthorizationPolicyBehaviorTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(BookifyRoles.Admin, true)]
    [InlineData(BookifyRoles.Owner, false)]
    [InlineData(BookifyRoles.Customer, false)]
    public async Task AdminOnly_ShouldAllowOnlyAdmin(
        string role,
        bool expectedSuccess)
    {
        // Arrange
        using IServiceScope scope =
            _factory.Services.CreateScope();

        IAuthorizationService authorizationService =
            scope.ServiceProvider
                .GetRequiredService<
                    IAuthorizationService>();

        ClaimsPrincipal principal =
            TestIdentityTokens.CreatePrincipal(
                $"policy-{Guid.NewGuid():N}",
                role);

        // Act
        AuthorizationResult result =
            await authorizationService.AuthorizeAsync(
                principal,
                resource: null,
                BookifyAuthorizationPolicies.AdminOnly);

        // Assert
        Assert.Equal(
            expectedSuccess,
            result.Succeeded);
    }

    [Fact]
    public async Task BookingOwner_WithCorrectCustomer_ShouldSucceed()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await BookingDatabaseTestSeeder
                .SeedBookingAsync(
                    _factory,
                    customerSubjectId:
                        TestIdentitySubjects.Customer,
                    cancellationToken:
                        cancellationToken);

        ClaimsPrincipal principal =
            TestIdentityTokens.CreatePrincipal(
                TestIdentitySubjects.Customer,
                BookifyRoles.Customer);

        // Act
        AuthorizationResult result =
            await AuthorizeBookingAsync(
                booking,
                principal,
                BookifyAuthorizationPolicies.BookingOwner,
                guestAccessToken: null);

        // Assert
        Assert.True(
            result.Succeeded);
    }

    [Fact]
    public async Task BookingOwner_WithDifferentCustomer_ShouldFail()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            await BookingDatabaseTestSeeder
                .SeedBookingAsync(
                    _factory,
                    customerSubjectId:
                        TestIdentitySubjects.Customer,
                    cancellationToken:
                        cancellationToken);

        ClaimsPrincipal principal =
            TestIdentityTokens.CreatePrincipal(
                $"different-customer-{Guid.NewGuid():N}",
                BookifyRoles.Customer);

        // Act
        AuthorizationResult result =
            await AuthorizeBookingAsync(
                booking,
                principal,
                BookifyAuthorizationPolicies.BookingOwner,
                guestAccessToken: null);

        // Assert
        Assert.False(
            result.Succeeded);
    }

    [Fact]
    public async Task BookingGuestAccess_WithValidCredential_ShouldSucceed()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        GuestBookingSeedData guest =
            await BookingDatabaseTestSeeder
                .SeedGuestBookingAsync(
                    _factory,
                    cancellationToken:
                        cancellationToken);

        ClaimsPrincipal principal =
            TestIdentityTokens
                .CreateAnonymousPrincipal();

        // Act
        AuthorizationResult result =
            await AuthorizeBookingAsync(
                guest.Booking,
                principal,
                BookifyAuthorizationPolicies
                    .BookingGuestAccess,
                guest.GuestAccessToken);

        // Assert
        Assert.True(
            result.Succeeded);
    }

    [Fact]
    public async Task BookingGuestAccess_WithInvalidCredential_ShouldFail()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        GuestBookingSeedData guest =
            await BookingDatabaseTestSeeder
                .SeedGuestBookingAsync(
                    _factory,
                    cancellationToken:
                        cancellationToken);

        ClaimsPrincipal principal =
            TestIdentityTokens
                .CreateAnonymousPrincipal();

        // Act
        AuthorizationResult result =
            await AuthorizeBookingAsync(
                guest.Booking,
                principal,
                BookifyAuthorizationPolicies
                    .BookingGuestAccess,
                new string('x', 43));

        // Assert
        Assert.False(
            result.Succeeded);
    }

    private async Task<AuthorizationResult>
        AuthorizeBookingAsync(
            DomainBooking booking,
            ClaimsPrincipal principal,
            string policyName,
            string? guestAccessToken)
    {
        using IServiceScope scope =
            _factory.Services.CreateScope();

        IHttpContextAccessor accessor =
            scope.ServiceProvider
                .GetRequiredService<
                    IHttpContextAccessor>();

        IAuthorizationService authorizationService =
            scope.ServiceProvider
                .GetRequiredService<
                    IAuthorizationService>();

        Endpoint endpoint =
            GetEndpoint(
                CancelEndpointName);

        var httpContext =
            new DefaultHttpContext
            {
                RequestServices =
                    scope.ServiceProvider,

                User =
                    principal
            };

        httpContext.SetEndpoint(
            endpoint);

        httpContext.Request.RouteValues[
            "bookingId"] =
                booking.Id.ToString("D");

        if (guestAccessToken is not null)
        {
            httpContext.Request.Headers[
                GuestTokenHeader] =
                    guestAccessToken;
        }

        accessor.HttpContext =
            httpContext;

        try
        {
            return await authorizationService
                .AuthorizeAsync(
                    principal,
                    httpContext,
                    policyName);
        }
        finally
        {
            accessor.HttpContext =
                null;
        }
    }

    private Endpoint GetEndpoint(
        string endpointName)
    {
        EndpointDataSource endpointDataSource =
            _factory.Services
                .GetRequiredService<
                    EndpointDataSource>();

        return Assert.Single(
            endpointDataSource.Endpoints,
            endpoint =>
                string.Equals(
                    endpoint.Metadata
                        .GetMetadata<
                            IEndpointNameMetadata>()?
                        .EndpointName,
                    endpointName,
                    StringComparison.Ordinal));
    }
}
