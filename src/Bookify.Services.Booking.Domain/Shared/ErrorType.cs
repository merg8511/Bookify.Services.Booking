namespace Bookify.Services.Booking.Domain.Shared;

public enum ErrorType
{
    None = 0,
    Failure = 1,
    Validation = 2,
    NotFound = 3,
    Conflict = 4
}
