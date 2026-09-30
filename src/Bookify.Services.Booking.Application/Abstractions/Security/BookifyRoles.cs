namespace Bookify.Services.Booking.Application.Abstractions.Security;

public static class BookifyRoles
{
    public const string ClaimType = BookifyClaimTypes.Roles;
    public const string Admin = "Admin";
    public const string Owner = "Owner";
    public const string Customer = "Customer";
}
