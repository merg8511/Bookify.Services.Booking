using System.Security.Claims;
using Bookify.Services.Booking.Application.Abstractions.Security;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.Security;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class CurrentUserAbstractionTests
{
    private readonly BookingApiFactory _factory;

    public CurrentUserAbstractionTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void CurrentUser_WithoutHttpContext_ShouldBeAnonymous()
    {
        // Arrange
        using CurrentUserTestScope context = CreateCurrentUserScope(principal: null);

        // Act
        ICurrentUser currentUser = context.CurrentUser;

        // Assert
        Assert.False(currentUser.IsAuthenticated);
        Assert.Null(currentUser.Subject);
        Assert.Empty(currentUser.Roles);
    }

    [Fact]
    public void CurrentUser_WithAuthenticatedPrincipal_ShouldExposeSubjectAndRoles()
    {
        // Arrange
        ClaimsPrincipal principal = TestIdentityTokens.CreatePrincipal("customer-123", BookifyRoles.Customer, BookifyRoles.Owner);

        using CurrentUserTestScope context = CreateCurrentUserScope(principal);

        // Act
        ICurrentUser currentUser = context.CurrentUser;

        // Assert
        Assert.True(currentUser.IsAuthenticated);
        Assert.Equal("customer-123", currentUser.Subject);

        Assert.Collection(currentUser.Roles,
            role => Assert.Equal(BookifyRoles.Customer, role),
            role => Assert.Equal(BookifyRoles.Owner, role));
    }

    [Fact]
    public void CurrentUser_WithUnauthenticatedPrincipal_ShouldNotTrustClaims()
    {
        // Arrange
        ClaimsPrincipal principal = TestIdentityTokens.CreateAnonymousPrincipal("spoofed-subject", BookifyRoles.Admin);

        using CurrentUserTestScope context = CreateCurrentUserScope(principal);

        // Act
        ICurrentUser currentUser = context.CurrentUser;

        // Assert
        Assert.False(currentUser.IsAuthenticated);
        Assert.Null(currentUser.Subject);
        Assert.Empty(currentUser.Roles);
    }

    private CurrentUserTestScope CreateCurrentUserScope(ClaimsPrincipal? principal)
    {
        IServiceScope scope = _factory.Services.CreateScope();

        IHttpContextAccessor accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

        accessor.HttpContext = principal is null ? null : new DefaultHttpContext
        {
            User = principal
        };

        ICurrentUser currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

        return new CurrentUserTestScope(scope, accessor, currentUser);
    }

    private sealed class CurrentUserTestScope : IDisposable
    {
        private readonly IServiceScope _scope;
        private readonly IHttpContextAccessor _accessor;

        public CurrentUserTestScope(IServiceScope scope, IHttpContextAccessor accessor, ICurrentUser currentUser)
        {
            _scope = scope;
            _accessor = accessor;
            CurrentUser = currentUser;
        }

        public ICurrentUser CurrentUser { get; }

        public void Dispose()
        {
            _accessor.HttpContext = null;

            _scope.Dispose();
        }
    }
}
