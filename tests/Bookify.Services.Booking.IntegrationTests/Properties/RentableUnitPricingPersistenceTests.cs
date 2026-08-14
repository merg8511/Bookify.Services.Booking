using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.IntegrationTests.Properties;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class RentableUnitPricingPersistenceTests
{
    private readonly BookingApiFactory _factory;

    public RentableUnitPricingPersistenceTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SaveAndReload_WithConfiguredPricing_ShouldPreservePricing()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Property property =
            Property.Create(
                "Pricing Persistence Property",
                "America/El_Salvador",
                new TimeOnly(
                    15,
                    0),
                new TimeOnly(
                    11,
                    0))
            .Value;

        RentableUnit rentableUnit =
            RentableUnit.Create(
                property.Id,
                "Room A",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2)
            .Value;

        RentableUnitPricing pricing =
            RentableUnitPricing.Create(
                Money.Create(
                    100m,
                    "USD")
                .Value,
                Money.Create(
                    140m,
                    "USD")
                .Value,
                Money.Create(
                    25m,
                    "USD")
                .Value)
            .Value;

        rentableUnit.ConfigurePricing(
            pricing);

        using (
            IServiceScope seedScope =
                _factory.Services.CreateScope())
        {
            IPropertyRepository propertyRepository =
                seedScope
                    .ServiceProvider
                    .GetRequiredService<
                        IPropertyRepository>();

            IRentableUnitRepository rentableUnitRepository =
                seedScope
                    .ServiceProvider
                    .GetRequiredService<
                        IRentableUnitRepository>();

            IUnitOfWork unitOfWork =
                seedScope
                    .ServiceProvider
                    .GetRequiredService<
                        IUnitOfWork>();

            propertyRepository.Add(
                property);

            rentableUnitRepository.Add(
                rentableUnit);

            await unitOfWork.SaveChangesAsync(
                cancellationToken);
        }

        // ACT
        RentableUnit? persistedRentableUnit;

        using (
            IServiceScope assertionScope =
                _factory.Services.CreateScope())
        {
            IRentableUnitRepository rentableUnitRepository =
                assertionScope
                    .ServiceProvider
                    .GetRequiredService<
                        IRentableUnitRepository>();

            persistedRentableUnit =
                await rentableUnitRepository
                    .GetByIdAsync(
                        rentableUnit.Id,
                        cancellationToken);
        }

        // ASSERT
        Assert.NotNull(
            persistedRentableUnit);

        Assert.NotNull(
            persistedRentableUnit.Pricing);

        Assert.Equal(
            100m,
            persistedRentableUnit
                .Pricing
                .RegularNightlyRate
                .Amount);

        Assert.Equal(
            "USD",
            persistedRentableUnit
                .Pricing
                .RegularNightlyRate
                .Currency);

        Assert.Equal(
            140m,
            persistedRentableUnit
                .Pricing
                .WeekendNightlyRate
                .Amount);

        Assert.Equal(
            "USD",
            persistedRentableUnit
                .Pricing
                .WeekendNightlyRate
                .Currency);

        Assert.Equal(
            25m,
            persistedRentableUnit
                .Pricing
                .ExtraGuestNightlyRate
                .Amount);

        Assert.Equal(
            "USD",
            persistedRentableUnit
                .Pricing
                .ExtraGuestNightlyRate
                .Currency);
    }
}
