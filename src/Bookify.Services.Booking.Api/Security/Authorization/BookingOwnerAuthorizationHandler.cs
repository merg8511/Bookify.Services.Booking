using Microsoft.AspNetCore.Authorization;

namespace Bookify.Services.Booking.Api.Security.Authorization;

internal sealed class BookingOwnerAuthorizationHandler :
    AuthorizationHandler<BookingOwnerRequirement>
{
    private readonly BookingAccessEvaluator _bookingAccessEvaluator;

    public BookingOwnerAuthorizationHandler(BookingAccessEvaluator bookingAccessEvaluator)
    {
        _bookingAccessEvaluator = bookingAccessEvaluator
            ?? throw new ArgumentNullException(nameof(bookingAccessEvaluator));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        BookingOwnerRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext)
        {
            return;
        }

        if (await _bookingAccessEvaluator.IsBookingOwnerAsync(httpContext))
        {
            context.Succeed(requirement);
        }
    }
}
