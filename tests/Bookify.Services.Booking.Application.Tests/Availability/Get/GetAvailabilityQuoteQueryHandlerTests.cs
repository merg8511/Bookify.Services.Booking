using Bookify.Services.Booking.Application.Availability;
using Bookify.Services.Booking.Application.Availability.Get;
using Bookify.Services.Booking.Application.Availability.ReadModels;
using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Application.Common.Sorting;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.Properties.GetPaged;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Availability.Get;

public sealed class GetAvailabilityQuoteQueryHandlerTests
{
    [Fact]
    public async Task
        HandleAsync_ShouldCalculateInformativeQuoteUsingPricingEngine()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        Guid propertyId =
            Guid.NewGuid();

        Guid unitId =
            Guid.NewGuid();

        var property =
            new PropertyDetailsReadModel
            {
                Id =
                    propertyId,

                Name =
                    "Quote Property",

                TimeZoneId =
                    "America/El_Salvador",

                CheckInTime =
                    new TimeOnly(
                        15,
                        0),

                CheckOutTime =
                    new TimeOnly(
                        11,
                        0),

                IsActive =
                    true
            };

        var availableUnit =
            new AvailableRentableUnitReadModel
            {
                Id =
                    unitId,

                PropertyId =
                    propertyId,

                Name =
                    "Room A",

                Type =
                    "Room",

                MaximumCapacity =
                    4,

                MaxBaseGuests =
                    2,

                IsEntireProperty =
                    false,

                RegularNightlyRateAmount =
                    100m,

                WeekendNightlyRateAmount =
                    140m,

                ExtraGuestNightlyRateAmount =
                    25m,

                PricingCurrency =
                    "USD"
            };

        var season =
            new AvailabilityPricingSeasonReadModel
            {
                RentableUnitId =
                    unitId,

                StartDate =
                    new DateOnly(
                        2026,
                        12,
                        25),

                EndDate =
                    new DateOnly(
                        2026,
                        12,
                        26),

                NightlyRateAmount =
                    200m,

                Currency =
                    "USD",

                Priority =
                    10
            };

        var handler =
            new GetAvailabilityQueryHandler(
                new StubPropertyReadService(
                    property),
                new StubAvailabilityReadService(
                    [availableUnit],
                    [season]));

        var query =
            new GetAvailabilityQuery(
                propertyId,
                new DateOnly(
                    2026,
                    12,
                    24),
                new DateOnly(
                    2026,
                    12,
                    27),
                GuestCount: 3);

        // Act
        Result<AvailabilityReadModel> result =
            await handler.HandleAsync(
                query,
                cancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        AvailableRentableUnitReadModel unit =
            Assert.Single(
                result.Value
                    .AvailableUnits);

        Assert.NotNull(
            unit.Quote);

        Assert.Equal(
            440m,
            unit.Quote
                .AccommodationPrice);

        Assert.Equal(
            75m,
            unit.Quote
                .ExtraGuestPrice);

        Assert.Equal(
            515m,
            unit.Quote
                .TotalPrice);

        Assert.Equal(
            "USD",
            unit.Quote
                .Currency);
    }

    private sealed class
        StubAvailabilityReadService :
            IAvailabilityReadService
    {
        private readonly
            IReadOnlyList<
                AvailableRentableUnitReadModel>
                _availableUnits;

        private readonly
            IReadOnlyList<
                AvailabilityPricingSeasonReadModel>
                _pricingSeasons;

        public StubAvailabilityReadService(
            IReadOnlyList<
                AvailableRentableUnitReadModel>
                availableUnits,
            IReadOnlyList<
                AvailabilityPricingSeasonReadModel>
                pricingSeasons)
        {
            _availableUnits =
                availableUnits;

            _pricingSeasons =
                pricingSeasons;
        }

        public Task<
            IReadOnlyList<
                AvailableRentableUnitReadModel>>
            GetAvailableUnitsAsync(
                Guid propertyId,
                DateOnly requestedCheckInDate,
                DateOnly requestedCheckOutDate,
                int guestCount,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                _availableUnits);
        }

        public Task<
            IReadOnlyList<
                AvailabilityPricingSeasonReadModel>>
            GetPricingSeasonsAsync(
                IReadOnlyCollection<Guid> rentableUnitIds,
                DateOnly requestedCheckInDate,
                DateOnly requestedCheckOutDate,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                _pricingSeasons);
        }

        public Task<
            IReadOnlyList<
                OverlappingBookingReadModel>>
            GetInventoryConflictsAsync(
                Guid propertyId,
                Guid requestedRentableUnitId,
                DateOnly requestedCheckInDate,
                DateOnly requestedCheckOutDate,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class
        StubPropertyReadService :
            IPropertyReadService
    {
        private readonly
            PropertyDetailsReadModel?
                _property;

        public StubPropertyReadService(
            PropertyDetailsReadModel?
                property)
        {
            _property =
                property;
        }

        public Task<
            PropertyDetailsReadModel?>
            GetByIdAsync(
                Guid propertyId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                _property?.Id ==
                    propertyId
                    ? _property
                    : null);
        }

        public Task<
            PagedResult<
                PropertyListItemReadModel>>
            GetPagedAsync(
                int pageNumber,
                int pageSize,
                string? name,
                bool? isActive,
                PropertySortField sortField,
                SortDirection sortDirection,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
