using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

namespace Bookify.Services.Booking.Domain.Bookings.Pricing;

public sealed record PriceBreakdown
{
    public PriceBreakdown(
        Money accommodationPrice,
        Money extraGuestPrice,
        Money totalPrice)
    {
        AccommodationPrice = accommodationPrice;
        ExtraGuestPrice = extraGuestPrice;
        TotalPrice = totalPrice;
    }

    public Money AccommodationPrice { get; }
    public Money ExtraGuestPrice { get; }
    public Money TotalPrice { get; }

    public static Result<PriceBreakdown> Create(
        Money accomodationPrice,
        Money extraGuestPrice)
    {
        ArgumentNullException.ThrowIfNull(accomodationPrice);
        ArgumentNullException.ThrowIfNull(extraGuestPrice);

        Result<Money> totalPriceResult =
            accomodationPrice.Add(extraGuestPrice);

        if (totalPriceResult.IsFailure)
        {
            return Result<PriceBreakdown>.Failure(totalPriceResult.Error);
        }

        return Result<PriceBreakdown>.Success(
            new PriceBreakdown(
                accomodationPrice,
                extraGuestPrice,
                totalPriceResult.Value));
    }
}
