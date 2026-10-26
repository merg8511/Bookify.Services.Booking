namespace Bookify.Services.Booking.Application.Abstractions.Persistence;

public sealed class OptimisticConcurrencyException : Exception
{
    public OptimisticConcurrencyException(Exception innerException) :
        base("The entity was modified concurrently.", innerException)
    {
    }
}
