using Microsoft.AspNetCore.Authorization;

namespace Bookify.Services.Booking.Api.Security.Authorization;

internal sealed class CustomerOrGuestBookingAccessAuthorizationHandler :
    AuthorizationHandler<CustomerOrGuestBookingAccessRequirement>
{
    private readonly BookingAccessEvaluator _bookingAccessEvaluator;

    public CustomerOrGuestBookingAccessAuthorizationHandler(BookingAccessEvaluator bookingAccessEvaluator)
    {
        _bookingAccessEvaluator = bookingAccessEvaluator
            ?? throw new ArgumentNullException(nameof(bookingAccessEvaluator));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CustomerOrGuestBookingAccessRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext)
        {
            return;
        }

        if (await _bookingAccessEvaluator.HasCustomerOrGuestAccessAsync(httpContext))
        {
            context.Succeed(requirement);
        }
    }
}
