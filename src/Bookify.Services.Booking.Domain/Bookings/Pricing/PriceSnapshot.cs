using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Bookings.Pricing;

public sealed record PriceSnapshot
{
    private PriceSnapshot()
    {
        AccommodationPrice = null!;
        ExtraGuestPrice = null!;
        TotalPrice = null!;
    }

    private PriceSnapshot(
        Money accomodationPrice,
        Money extraGuestPrice,
        Money totalPrice)
    {
        AccommodationPrice = accomodationPrice;
        ExtraGuestPrice = extraGuestPrice;
        TotalPrice = totalPrice;
    }

    public Money AccommodationPrice { get; private set; }
    public Money ExtraGuestPrice { get; private set; }
    public Money TotalPrice { get; private set; }

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
