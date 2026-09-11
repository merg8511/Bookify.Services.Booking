using Bookify.Services.Booking.Application.Payments;
using Bookify.Services.Booking.Application.Payments.GetStatus;
using Bookify.Services.Booking.Application.Payments.ReadModels;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Tests.Payments.GetStatus;

public sealed class GetPaymentStatusQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenBookingExists_ShouldReturnPaymentStatus()
    {
        // Arrange
        Guid bookingId =
            Guid.NewGuid();

        var readModel =
            new PaymentStatusReadModel
            {
                BookingId =
                    bookingId,

                BookingStatus =
                    "PendingPayment",

                PaymentStatus =
                    "Pending",

                PaymentAttemptStatus =
                    "Pending"
            };

        var handler =
            new GetPaymentStatusQueryHandler(
                new StubPaymentReadService(
                    readModel));

        // Act
        Result<PaymentStatusReadModel> result =
            await handler.HandleAsync(
                new GetPaymentStatusQuery(
                    bookingId),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Same(
            readModel,
            result.Value);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingExistsWithoutPayment_ShouldReturnSuccessWithNullPaymentStatuses()
    {
        // Arrange
        Guid bookingId =
            Guid.NewGuid();

        var readModel =
            new PaymentStatusReadModel
            {
                BookingId =
                    bookingId,

                BookingStatus =
                    "PendingPayment",

                PaymentStatus =
                    null,

                PaymentAttemptStatus =
                    null
            };

        var handler =
            new GetPaymentStatusQueryHandler(
                new StubPaymentReadService(
                    readModel));

        // Act
        Result<PaymentStatusReadModel> result =
            await handler.HandleAsync(
                new GetPaymentStatusQuery(
                    bookingId),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsSuccess);

        Assert.Null(
            result.Value.PaymentStatus);

        Assert.Null(
            result.Value.PaymentAttemptStatus);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        Guid bookingId =
            Guid.NewGuid();

        var handler =
            new GetPaymentStatusQueryHandler(
                new StubPaymentReadService());

        // Act
        Result<PaymentStatusReadModel> result =
            await handler.HandleAsync(
                new GetPaymentStatusQuery(
                    bookingId),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            result.IsFailure);

        Assert.Equal(
            GetPaymentStatusErrors
                .NotFound(
                    bookingId),
            result.Error);
    }

    [Fact]
    public async Task HandleAsync_WithNullQuery_ShouldThrow()
    {
        // Arrange
        var handler =
            new GetPaymentStatusQueryHandler(
                new StubPaymentReadService());

        // Act
        Task Action()
        {
            return handler.HandleAsync(
                null!);
        }

        // Assert
        await Assert.ThrowsAsync<
            ArgumentNullException>(
                Action);
    }

    private sealed class StubPaymentReadService
        : IPaymentReadService
    {
        private readonly PaymentStatusReadModel?
            _paymentStatus;

        public StubPaymentReadService(
            PaymentStatusReadModel? paymentStatus = null)
        {
            _paymentStatus =
                paymentStatus;
        }

        public Task<PaymentStatusReadModel?>
            GetStatusByBookingIdAsync(
                Guid bookingId,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            PaymentStatusReadModel? result =
                _paymentStatus?.BookingId ==
                bookingId
                    ? _paymentStatus
                    : null;

            return Task.FromResult(
                result);
        }
    }
}
