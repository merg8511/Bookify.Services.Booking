using Bookify.Services.Booking.Application;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.Get;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Get;

public sealed class GetBookingQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithBookingId_ShouldReturnBooking()
    {
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        BookingDetailsReadModel booking =
            CreateReadModel(
                bookingId);

        var readService =
            new StubBookingReadService(
                booking);

        var handler =
            new GetBookingQueryHandler(
                readService);

        Result<BookingDetailsReadModel> result =
            await handler.HandleAsync(
                new GetBookingQuery(
                    bookingId.ToString()),
                cancellationToken);

        Assert.True(
            result.IsSuccess);

        Assert.Same(
            booking,
            result.Value);

        Assert.Equal(
            bookingId,
            readService.LastBookingId);
    }

    [Fact]
    public async Task HandleAsync_WithReference_ShouldNormalizeAndReturnBooking()
    {
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        BookingReference reference =
            BookingReference.New();

        BookingDetailsReadModel booking =
            CreateReadModel(
                Guid.NewGuid(),
                reference.Value);

        var readService =
            new StubBookingReadService(
                booking);

        var handler =
            new GetBookingQueryHandler(
                readService);

        Result<BookingDetailsReadModel> result =
            await handler.HandleAsync(
                new GetBookingQuery(
                    reference.Value
                        .ToLowerInvariant()),
                cancellationToken);

        Assert.True(
            result.IsSuccess);

        Assert.Same(
            booking,
            result.Value);

        Assert.Equal(
            reference.Value,
            readService.LastBookingReference);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingDoesNotExist_ShouldReturnNotFound()
    {
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        var handler =
            new GetBookingQueryHandler(
                new StubBookingReadService(
                    null));

        Result<BookingDetailsReadModel> result =
            await handler.HandleAsync(
                new GetBookingQuery(
                    bookingId.ToString()),
                cancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            "Booking.NotFound",
            result.Error.Code);

        Assert.Equal(
            ErrorType.NotFound,
            result.Error.Type);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidIdentifier_ShouldReturnValidationError()
    {
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        var handler =
            new GetBookingQueryHandler(
                new StubBookingReadService(
                    null));

        Result<BookingDetailsReadModel> result =
            await handler.HandleAsync(
                new GetBookingQuery(
                    "not-a-booking"),
                cancellationToken);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            GetBookingErrors.InvalidIdentifier,
            result.Error);
    }

    private static BookingDetailsReadModel
        CreateReadModel(
            Guid bookingId,
            string? bookingReference = null)
    {
        return new BookingDetailsReadModel
        {
            Id =
                bookingId,

            BookingReference =
                bookingReference ??
                BookingReference.New().Value
        };
    }

    private sealed class
        StubBookingReadService :
            IBookingReadService
    {
        private readonly
            BookingDetailsReadModel? _booking;

        public StubBookingReadService(
            BookingDetailsReadModel? booking)
        {
            _booking =
                booking;
        }

        public Guid? LastBookingId
        {
            get;
            private set;
        }

        public string? LastBookingReference
        {
            get;
            private set;
        }

        public Task<
            BookingDetailsReadModel?>
            GetByIdAsync(
                Guid bookingId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LastBookingId =
                bookingId;

            BookingDetailsReadModel? result =
                _booking?.Id == bookingId
                    ? _booking
                    : null;

            return Task.FromResult(
                result);
        }

        public Task<
            BookingDetailsReadModel?>
            GetByReferenceAsync(
                string bookingReference,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LastBookingReference =
                bookingReference;

            BookingDetailsReadModel? result =
                string.Equals(
                    _booking?.BookingReference,
                    bookingReference,
                    StringComparison.Ordinal)
                    ? _booking
                    : null;

            return Task.FromResult(
                result);
        }

        public Task<
            IReadOnlyList<
                BookingCalendarItemReadModel>>
            GetCalendarAsync(
                Guid propertyId,
                DateOnly rangeStart,
                DateOnly rangeEnd,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            IReadOnlyList<
                BookingCalendarItemReadModel> result =
                    Array.Empty<
                        BookingCalendarItemReadModel>();

            return Task.FromResult(
                result);
        }
    }
}
