using Microsoft.AspNetCore.Authorization;

namespace Bookify.Services.Booking.Api.Security.Authorization;

internal sealed class BookingGuestAccessAuthorizationHandler :
    AuthorizationHandler<BookingGuestAccessRequirement>
{
    private readonly BookingAccessEvaluator _bookingAccessEvaluator;

    public BookingGuestAccessAuthorizationHandler(BookingAccessEvaluator bookingAccessEvaluator)
    {
        _bookingAccessEvaluator = bookingAccessEvaluator
            ?? throw new ArgumentNullException(nameof(bookingAccessEvaluator));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        BookingGuestAccessRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext)
        {
            return;
        }

        if (await _bookingAccessEvaluator.HasGuestAccessAsync(httpContext))
        {
            context.Succeed(requirement);
        }
    }
}
