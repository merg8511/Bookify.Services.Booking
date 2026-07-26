namespace Bookify.Services.Booking.IntegrationTests.Infrastructure;

[CollectionDefinition("Booking API",
    DisableParallelization = true)]
public sealed class BookingApiTestFixture :
    ICollectionFixture<BookingApiFactory>
{
    public const string Name = "Booking API";
}
