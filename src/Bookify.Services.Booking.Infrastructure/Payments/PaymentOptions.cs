namespace Bookify.Services.Booking.Infrastructure.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    public string Provider { get; init; } = string.Empty;
}
