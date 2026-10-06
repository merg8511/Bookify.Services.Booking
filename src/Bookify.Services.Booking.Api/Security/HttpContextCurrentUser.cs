using Bookify.Services.Booking.Application.Abstractions.Security;
using System.Security.Claims;

namespace Bookify.Services.Booking.Api.Security;

internal sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ??
            throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public string? Subject
    {
        get
        {
            if (!IsAuthenticated)
            {
                return null;
            }

            return User?.FindFirst(BookifyClaimTypes.Subject)?.Value;
        }
    }

    public IReadOnlyCollection<string> Roles
    {
        get
        {
            ClaimsPrincipal? user = User;

            if (user?.Identity?.IsAuthenticated != true)
            {
                return Array.Empty<string>();
            }

            return user
                .FindAll(BookifyRoles.ClaimType)
                .Select(claim => claim.Value)
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;
}
