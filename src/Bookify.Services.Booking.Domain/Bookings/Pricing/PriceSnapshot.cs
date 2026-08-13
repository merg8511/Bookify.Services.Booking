using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Bookings.Pricing;

public sealed record PriceSnapshot
{
    private PriceSnapshot(
        Money accomodationPrice,
        Money extraGuestPrice,
        Money totalPrice)
    {
        AccommodationPrice = accomodationPrice;
        ExtraGuestPrice = extraGuestPrice;
        TotalPrice = totalPrice;
    }

    public Money AccommodationPrice { get; }
    public Money ExtraGuestPrice { get; }
    public Money TotalPrice { get; }

    public static PriceSnapshot Create(
        PriceBreakdown priceBreakdown)
    {
        ArgumentNullException.ThrowIfNull(priceBreakdown);

        return new PriceSnapshot(
            priceBreakdown.AccommodationPrice,
            priceBreakdown.ExtraGuestPrice,
            priceBreakdown.TotalPrice);
    }
}
