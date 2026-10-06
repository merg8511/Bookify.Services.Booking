namespace Bookify.Services.Booking.Integration.Tests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BookingApiTestFixture : ICollectionFixture<BookingApiFactory>
{
    public const string Name = "Booking API";
}
