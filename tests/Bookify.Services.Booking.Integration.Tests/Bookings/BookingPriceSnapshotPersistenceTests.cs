using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings.Pricing;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class BookingPriceSnapshotPersistenceTests
{
    private readonly BookingApiFactory _factory;

    public BookingPriceSnapshotPersistenceTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SaveAndReload_WithPriceSnapshot_ShouldPreserveSnapshot()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        StayPeriod stayPeriod = StayPeriod.Create(
            new DateOnly(2026, 12, 24),
            new DateOnly(2026, 12, 28)).Value;

        GuestCount guestCount = GuestCount.Create(4).Value;

        PriceBreakdown priceBreakdown = PriceBreakdown.Create(
            Money.Create(700m, "USD").Value,
            Money.Create(200m, "USD").Value).Value;

        PriceSnapshot priceSnapshot = PriceSnapshot.Create(priceBreakdown);

        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory.Services,
            stayPeriod: stayPeriod,
            guestCount: guestCount,
            priceSnapshot: priceSnapshot,
            cancellationToken: cancellationToken);

        // ACT
        DomainBooking? persistedBooking;

        using (IServiceScope assertionScope = _factory.Services.CreateScope())
        {
            IBookingRepository bookingRepository = assertionScope.ServiceProvider
                .GetRequiredService<IBookingRepository>();

            persistedBooking = await bookingRepository.GetByIdAsync(booking.Id, cancellationToken);
        }

        // ASSERT
        Assert.NotNull(persistedBooking);
        Assert.NotNull(persistedBooking.PriceSnapshot);
        Assert.Equal(700m, persistedBooking.PriceSnapshot.AccommodationPrice.Amount);
        Assert.Equal("USD", persistedBooking.PriceSnapshot.AccommodationPrice.Currency);
        Assert.Equal(200m, persistedBooking.PriceSnapshot.ExtraGuestPrice.Amount);
        Assert.Equal("USD", persistedBooking.PriceSnapshot.ExtraGuestPrice.Currency);
        Assert.Equal(900m, persistedBooking.PriceSnapshot.TotalPrice.Amount);
        Assert.Equal("USD", persistedBooking.PriceSnapshot.TotalPrice.Currency);
    }
}
