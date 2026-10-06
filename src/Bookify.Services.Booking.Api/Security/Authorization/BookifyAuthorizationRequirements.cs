using Microsoft.AspNetCore.Authorization;

namespace Bookify.Services.Booking.Api.Security.Authorization;

internal sealed class PropertyOwnerOrAdminRequirement : IAuthorizationRequirement
{
}

internal sealed class BookingOwnerRequirement : IAuthorizationRequirement
{
}

internal sealed class BookingGuestAccessRequirement : IAuthorizationRequirement
{
}

internal sealed class CustomerOrGuestBookingAccessRequirement : IAuthorizationRequirement
{
}
