
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.Administrative;
using Bookify.Services.Booking.Application.Bookings.Administrative.GetById;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Application.Common.Sorting;
using Bookify.Services.Booking.Application.Payments.Administrative;
using Bookify.Services.Booking.Application.Payments.Administrative.GetByBooking;
using Bookify.Services.Booking.Application.Payments.Administrative.ReadModels;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.Properties.GetPaged;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Administrative;

public sealed class AdminBookingApplicationBoundaryTests
{
    [Fact]
    public async Task GetBooking_WhenPropertyDoesNotExist_ShouldNotReadBooking()
    {
        // ARRANGE
        Guid propertyId = Guid.NewGuid();
        Guid bookingId = Guid.NewGuid();

        var properties = new StubPropertyReadService(null);
        var bookings = new StubBookingReadService(null);

        var handler = new GetAdminBookingByIdQueryHandler(
            properties,
            bookings);

        // ACT
        Result<BookingDetailsReadModel> result =
            await handler.HandleAsync(
                new GetAdminBookingByIdQuery(propertyId, bookingId),
                TestContext.Current.CancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);
        Assert.Equal(
            AdminBookingErrors.PropertyNotFound(propertyId),
            result.Error);

        Assert.Equal(1, properties.CallCount);
        Assert.Equal(0, bookings.CallCount);
    }

    [Fact]
    public async Task GetBooking_WhenBookingBelongsToAnotherProperty_ShouldNotReturnDetails()
    {
        // ARRANGE
        Guid requestedPropertyId = Guid.NewGuid();
        Guid foreignPropertyId = Guid.NewGuid();
        Guid bookingId = Guid.NewGuid();

        var properties = new StubPropertyReadService(
            CreateProperty(requestedPropertyId));

        var bookings = new StubBookingReadService(
            new BookingDetailsReadModel
            {
                Id = bookingId,
                PropertyId = foreignPropertyId,
                Status = "PendingApproval"
            });

        var handler = new GetAdminBookingByIdQueryHandler(
            properties,
            bookings);

        // ACT
        Result<BookingDetailsReadModel> result =
            await handler.HandleAsync(
                new GetAdminBookingByIdQuery(
                    requestedPropertyId,
                    bookingId),
                TestContext.Current.CancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);
        Assert.Equal(
            AdminBookingErrors.BookingNotFound(bookingId),
            result.Error);

        Assert.Equal(1, bookings.CallCount);
        Assert.Equal(bookingId, bookings.RequestedBookingId);
    }

    [Fact]
    public async Task GetBooking_WhenBookingBelongsToProperty_ShouldReturnDetails()
    {
        // ARRANGE
        Guid propertyId = Guid.NewGuid();
        Guid bookingId = Guid.NewGuid();

        BookingDetailsReadModel booking = new()
        {
            Id = bookingId,
            PropertyId = propertyId,
            BookingReference = "BK-TEST-001",
            Status = "PendingApproval"
        };

        var properties = new StubPropertyReadService(
            CreateProperty(propertyId));

        var bookings = new StubBookingReadService(booking);

        var handler = new GetAdminBookingByIdQueryHandler(
            properties,
            bookings);

        // ACT
        Result<BookingDetailsReadModel> result =
            await handler.HandleAsync(
                new GetAdminBookingByIdQuery(propertyId, bookingId),
                TestContext.Current.CancellationToken);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.Same(booking, result.Value);

        Assert.Equal(propertyId, properties.RequestedPropertyId);
        Assert.Equal(bookingId, bookings.RequestedBookingId);
    }

    [Fact]
    public async Task GetPayment_WhenPropertyDoesNotExist_ShouldNotReadPayment()
    {
        // ARRANGE
        Guid propertyId = Guid.NewGuid();
        Guid bookingId = Guid.NewGuid();

        var properties = new StubPropertyReadService(null);
        var payments = new StubAdminPaymentReadService(null);

        var handler = new GetAdminBookingPaymentQueryHandler(
            properties,
            payments);

        // ACT
        Result<AdminBookingPaymentReadModel> result =
            await handler.HandleAsync(
                new GetAdminBookingPaymentQuery(propertyId, bookingId),
                TestContext.Current.CancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);
        Assert.Equal(
            AdminBookingErrors.PropertyNotFound(propertyId),
            result.Error);

        Assert.Equal(0, payments.CallCount);
    }

    [Fact]
    public async Task GetPayment_WhenBookingDoesNotExist_ShouldReturnNotFound()
    {
        // ARRANGE
        Guid propertyId = Guid.NewGuid();
        Guid bookingId = Guid.NewGuid();

        var properties = new StubPropertyReadService(
            CreateProperty(propertyId));

        var payments = new StubAdminPaymentReadService(null);

        var handler = new GetAdminBookingPaymentQueryHandler(
            properties,
            payments);

        // ACT
        Result<AdminBookingPaymentReadModel> result =
            await handler.HandleAsync(
                new GetAdminBookingPaymentQuery(propertyId, bookingId),
                TestContext.Current.CancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);
        Assert.Equal(
            AdminBookingErrors.BookingNotFound(bookingId),
            result.Error);

        Assert.Equal(propertyId, payments.RequestedPropertyId);
        Assert.Equal(bookingId, payments.RequestedBookingId);
    }

    [Fact]
    public async Task GetPayment_WhenBookingHasNoPayment_ShouldReturnSuccessfulNullPayment()
    {
        // ARRANGE
        Guid propertyId = Guid.NewGuid();
        Guid bookingId = Guid.NewGuid();

        var properties = new StubPropertyReadService(
            CreateProperty(propertyId));

        AdminBookingPaymentReadModel readModel = new(
            bookingId,
            "PendingApproval",
            Payment: null);

        var payments = new StubAdminPaymentReadService(readModel);

        var handler = new GetAdminBookingPaymentQueryHandler(
            properties,
            payments);

        // ACT
        Result<AdminBookingPaymentReadModel> result =
            await handler.HandleAsync(
                new GetAdminBookingPaymentQuery(propertyId, bookingId),
                TestContext.Current.CancellationToken);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.Same(readModel, result.Value);
        Assert.Equal(bookingId, result.Value.BookingId);
        Assert.Null(result.Value.Payment);

        Assert.Equal(propertyId, payments.RequestedPropertyId);
        Assert.Equal(bookingId, payments.RequestedBookingId);
    }

    [Fact]
    public async Task GetBooking_WhenCancellationRequested_ShouldPropagateCancellation()
    {
        // ARRANGE
        Guid propertyId = Guid.NewGuid();
        Guid bookingId = Guid.NewGuid();

        var properties = new StubPropertyReadService(
            CreateProperty(propertyId));

        var bookings = new StubBookingReadService(null);

        var handler = new GetAdminBookingByIdQueryHandler(
            properties,
            bookings);

        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // ACT
        async Task Action() => await handler.HandleAsync(
            new GetAdminBookingByIdQuery(propertyId, bookingId),
            source.Token);

        // ASSERT
        await Assert.ThrowsAnyAsync<OperationCanceledException>(Action);

        Assert.Equal(0, bookings.CallCount);
    }

    private static PropertyDetailsReadModel CreateProperty(Guid id)
    {
        return new PropertyDetailsReadModel
        {
            Id = id,
            Name = "Casa Bookify",
            TimeZoneId = "America/El_Salvador",
            CheckInTime = new TimeOnly(15, 0),
            CheckOutTime = new TimeOnly(11, 0),
            IsActive = true
        };
    }

    private sealed class StubPropertyReadService : IPropertyReadService
    {
        private readonly PropertyDetailsReadModel? _response;

        public StubPropertyReadService(
            PropertyDetailsReadModel? response)
        {
            _response = response;
        }

        public int CallCount { get; private set; }
        public Guid? RequestedPropertyId { get; private set; }

        public Task<PropertyDetailsReadModel?> GetByIdAsync(
            Guid propertyId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;
            RequestedPropertyId = propertyId;

            return Task.FromResult(_response);
        }

        public Task<PagedResult<PropertyListItemReadModel>> GetPagedAsync(
            int pageNumber,
            int pageSize,
            string? name,
            bool? isActive,
            PropertySortField sortField,
            SortDirection sortDirection,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "Paged properties are outside this test.");
        }
    }

    private sealed class StubBookingReadService : IBookingReadService
    {
        private readonly BookingDetailsReadModel? _response;

        public StubBookingReadService(BookingDetailsReadModel? response)
        {
            _response = response;
        }

        public int CallCount { get; private set; }
        public Guid? RequestedBookingId { get; private set; }

        public Task<BookingDetailsReadModel?> GetByIdAsync(
            Guid bookingId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;
            RequestedBookingId = bookingId;

            return Task.FromResult(_response);
        }

        public Task<BookingDetailsReadModel?> GetByReferenceAsync(
            string bookingReference,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "Booking reference reads are outside this test.");
        }

        public Task<IReadOnlyList<BookingCalendarItemReadModel>> GetCalendarAsync(
            Guid propertyId,
            DateOnly rangeStart,
            DateOnly rangeEnd,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "Calendar reads are outside this test.");
        }
    }

    private sealed class StubAdminPaymentReadService :
        IAdminPaymentReadService
    {
        private readonly AdminBookingPaymentReadModel? _response;

        public StubAdminPaymentReadService(
            AdminBookingPaymentReadModel? response)
        {
            _response = response;
        }

        public int CallCount { get; private set; }
        public Guid? RequestedPropertyId { get; private set; }
        public Guid? RequestedBookingId { get; private set; }

        public Task<AdminBookingPaymentReadModel?> GetByBookingAsync(
            Guid propertyId,
            Guid bookingId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;
            RequestedPropertyId = propertyId;
            RequestedBookingId = bookingId;

            return Task.FromResult(_response);
        }
    }
}
