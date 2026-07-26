using Bookify.Services.Booking.Domain.Shared.Errors;

namespace Bookify.Services.Booking.Domain.Shared.ValueObjects;

public sealed record GuestCount
{
    private GuestCount()
    {
    }

    private GuestCount(int value)
    {
        Value = value;
    }

    public int Value { get; private set; }

    public static Result<GuestCount> Create(int value)
    {
        if (value <= 0)
        {
            return Result<GuestCount>.Failure(
                GuestCountErrors.InvalidValue);
        }

        return Result<GuestCount>.Success(
            new GuestCount(value));
    }
}
