namespace Bookify.Services.Booking.Api.Security.Authorization;

public static class BookifyAuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string PropertyOwnerOrAdmin = "PropertyOwnerOrAdmin";
    public const string BookingOwner = "BookingOwner";
    public const string BookingGuestAccess = "BookingGuestAccess";
    public const string CustomerOrGuestBookingAccess = "CustomerOrGuestBookingAccess";

}
