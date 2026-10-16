namespace Bookify.Services.Booking.Application.Tests.Observability;

[CollectionDefinition(
    Name,
    DisableParallelization = true)]
public sealed class BookingMetricsTestCollection
{
    public const string Name =
        "Booking Metrics Tests";
}
