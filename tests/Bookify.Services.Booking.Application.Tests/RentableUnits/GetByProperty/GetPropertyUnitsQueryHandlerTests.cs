using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Application.Common.Sorting;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.Properties.GetPaged;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Bookify.Services.Booking.Application.RentableUnits;
using Bookify.Services.Booking.Application.RentableUnits.GetByProperty;
using Bookify.Services.Booking.Application.RentableUnits.ReadModels;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.RentableUnits.GetByProperty;

public sealed class GetPropertyUnitsQueryHandlerTests
{
    [Fact]
    public async Task
        HandleAsync_WhenPropertyIsActive_ShouldReturnActiveUnits()
    {
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        Guid propertyId =
            Guid.NewGuid();

        var property =
            new PropertyDetailsReadModel
            {
                Id =
                    propertyId,

                Name =
                    "Rancho Costa Azul",

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

        RentableUnitListItemReadModel[] expectedUnits =
        [
            new()
            {
                Id =
                    Guid.NewGuid(),

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

                IsActive =
                    true,

                IsEntireProperty =
                    false
            }
        ];

        var unitReadService =
            new StubRentableUnitReadService(
                expectedUnits);

        var handler =
            new GetPropertyUnitsQueryHandler(
                new StubPropertyReadService(
                    property),
                unitReadService);

        Result<
            IReadOnlyList<
                RentableUnitListItemReadModel>>
            result =
                await handler.HandleAsync(
                    new GetPropertyUnitsQuery(
                        propertyId),
                    cancellationToken);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            expectedUnits,
            result.Value);

        Assert.True(
            unitReadService
                .ActiveUnitsRequested);

        Assert.Equal(
            propertyId,
            unitReadService
                .RequestedPropertyId);
    }

    [Fact]
    public async Task
        HandleAsync_WhenPropertyDoesNotExist_ShouldReturnNotFound()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Guid propertyId =
            Guid.NewGuid();

        var handler =
            new GetPropertyUnitsQueryHandler(
                new StubPropertyReadService(
                    null),
                new StubRentableUnitReadService(
                    []));

        Result<
            IReadOnlyList<
                RentableUnitListItemReadModel>>
            result =
                await handler.HandleAsync(
                    new GetPropertyUnitsQuery(
                        propertyId), cancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            "Property.NotFound",
            result.Error.Code);

        Assert.Equal(
            ErrorType.NotFound,
            result.Error.Type);
    }

    [Fact]
    public async Task
        HandleAsync_WhenPropertyIsInactive_ShouldReturnConflict()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid propertyId =
            Guid.NewGuid();

        var property =
            new PropertyDetailsReadModel
            {
                Id =
                    propertyId,

                Name =
                    "Inactive Property",

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
                    false
            };

        var unitReadService =
            new StubRentableUnitReadService(
                []);

        var handler =
            new GetPropertyUnitsQueryHandler(
                new StubPropertyReadService(
                    property),
                unitReadService);

        Result<
            IReadOnlyList<
                RentableUnitListItemReadModel>>
            result =
                await handler.HandleAsync(new GetPropertyUnitsQuery(propertyId), cancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            "Property.Inactive",
            result.Error.Code);

        Assert.Equal(
            ErrorType.Conflict,
            result.Error.Type);

        Assert.False(
            unitReadService
                .ActiveUnitsRequested);
    }

    private sealed class
        StubRentableUnitReadService :
            IRentableUnitReadService
    {
        private readonly
            IReadOnlyList<
                RentableUnitListItemReadModel>
                _units;

        public StubRentableUnitReadService(
            IReadOnlyList<
                RentableUnitListItemReadModel>
                units)
        {
            _units =
                units;
        }

        public bool ActiveUnitsRequested
        {
            get;
            private set;
        }

        public Guid? RequestedPropertyId
        {
            get;
            private set;
        }

        public Task<
            IReadOnlyList<
                RentableUnitListItemReadModel>>
            GetByPropertyIdAsync(
                Guid propertyId,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<
            IReadOnlyList<
                RentableUnitListItemReadModel>>
            GetActiveByPropertyIdAsync(
                Guid propertyId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            ActiveUnitsRequested =
                true;

            RequestedPropertyId =
                propertyId;

            return Task.FromResult(
                _units);
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

            PropertyDetailsReadModel? result =
                _property?.Id ==
                    propertyId
                    ? _property
                    : null;

            return Task.FromResult(
                result);
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
