namespace Bookify.Services.Booking.Integration.Tests.Infrastructure;

[CollectionDefinition("Booking API",
    DisableParallelization = true)]
public sealed class BookingApiTestFixture :
    ICollectionFixture<BookingApiFactory>
{
    public const string Name = "Booking API";
}
