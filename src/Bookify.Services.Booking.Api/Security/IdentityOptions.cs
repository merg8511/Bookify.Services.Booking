namespace Bookify.Services.Booking.Api.Security;

internal sealed class IdentityOptions
{
    public const string SectionName = "Identity";
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}
