using Bookify.Services.Booking.Application.Abstractions.Security;

namespace Bookify.Services.Booking.Api.Security.Authorization;

internal sealed class BookingAccessEvaluator
{
    private readonly ICurrentUser _currentUser;
    private readonly BookingAuthorizationResolver _bookingResolver;

    public BookingAccessEvaluator(ICurrentUser currentUser, BookingAuthorizationResolver bookingResolver)
    {
        _currentUser = currentUser
            ?? throw new ArgumentNullException(nameof(currentUser));

        _bookingResolver = bookingResolver
            ?? throw new ArgumentNullException(nameof(bookingResolver));
    }

    public async Task<bool> IsBookingOwnerAsync(HttpContext httpContext)
    {
        if (!HasCustomerIdentity())
        {
            return false;
        }

        BookingAuthorizationResolution resolution = await _bookingResolver.ResolveAsync(httpContext);

        if (!resolution.HasMetadata)
        {
            return false;
        }

        if (!resolution.HasIdentifier)
        {
            return true;
        }

        if (resolution.Booking is null)
        {
            return true;
        }

        return string.Equals(
            resolution.Booking.CustomerSubjectId,
            _currentUser.Subject,
            StringComparison.Ordinal);
    }

    public async Task<bool> HasGuestAccessAsync(HttpContext httpContext)
    {
        string? guestToken = BookingGuestAccessHeader.GetToken(httpContext.Request);

        if (guestToken is null)
        {
            return false;
        }

        BookingAuthorizationResolution resolution = await _bookingResolver.ResolveAsync(httpContext);

        if (!resolution.HasMetadata)
        {
            return false;
        }

        if (!resolution.HasIdentifier)
        {
            return true;
        }

        if (resolution.Booking is null)
        {
            return true;
        }

        return resolution.Booking.VerifyGuestAccessToken(guestToken);
    }

    public async Task<bool> HasCustomerOrGuestAccessAsync(HttpContext httpContext)
    {
        bool hasCustomerIdentity = HasCustomerIdentity();
        string? guestToken = BookingGuestAccessHeader.GetToken(httpContext.Request);

        bool hasGuestCredential = guestToken is not null;

        if (!hasCustomerIdentity && !hasGuestCredential)
        {
            return false;
        }

        BookingAuthorizationResolution resolution = await _bookingResolver.ResolveAsync(httpContext);


        if (!resolution.HasMetadata)
        {
            return false;
        }

        if (!resolution.HasIdentifier)
        {
            return true;
        }

        if (resolution.Booking is null)
        {
            return true;
        }

        if (resolution.Booking is null)
        {
            return true;
        }

        if (hasCustomerIdentity &&
            string.Equals(resolution.Booking.CustomerSubjectId, _currentUser.Subject, StringComparison.Ordinal))
        {
            return true;
        }

        return hasGuestCredential && resolution.Booking.VerifyGuestAccessToken(guestToken);
    }

    private bool HasCustomerIdentity()
    {
        return _currentUser.IsAuthenticated &&
            !string.IsNullOrWhiteSpace(_currentUser.Subject) &&
            _currentUser.Roles.Contains(BookifyRoles.Customer, StringComparer.Ordinal);
    }
}
