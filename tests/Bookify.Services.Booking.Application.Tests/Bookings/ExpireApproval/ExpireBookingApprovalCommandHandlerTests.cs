using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.ExpireApproval;
using Bookify.Services.Booking.Application.Tests.Infrastructure;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Shared;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Tests.Bookings.ExpireApproval;

public sealed class ExpireBookingApprovalCommandHandlerTests
{
    private static readonly DateTimeOffset UtcNow =
        new(
            2026,
            9,
            9,
            18,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_WhenApprovalIsExpired_ShouldCancelBooking()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            BookingTestFactory.CreateValidBooking();

        var unitOfWork = new SpyUnitOfWork();

        var handler =
            new ExpireBookingApprovalCommandHandler(
                new StubBookingRepository(booking),
                unitOfWork,
                new FixedClock(UtcNow));

        Result result =
            await handler.HandleAsync(
                new ExpireBookingApprovalCommand(
                    booking.Id),
                cancellationToken);

        Assert.True(result.IsSuccess);

        Assert.Equal(
            BookingStatus.Cancelled,
            booking.Status);

        Assert.Equal(
            BookingCancellationReason.ApprovalExpired,
            booking.CancellationReason);

        Assert.Equal(
            UtcNow,
            booking.CancelledAtUtc);

        Assert.False(booking.BlocksInventory);

        Assert.Equal(
            1,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenApprovalDeadlineHasNotExpired_ShouldNoOp()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            BookingTestFactory.CreateValidBooking();

        DateTimeOffset beforeDeadline =
            BookingTestTime.ApprovalDueAtUtc
                .AddMinutes(-1);

        var unitOfWork = new SpyUnitOfWork();

        var handler =
            new ExpireBookingApprovalCommandHandler(
                new StubBookingRepository(booking),
                unitOfWork,
                new FixedClock(beforeDeadline));

        Result result =
            await handler.HandleAsync(
                new ExpireBookingApprovalCommand(
                    booking.Id),
                cancellationToken);

        Assert.True(result.IsSuccess);

        Assert.Equal(
            BookingStatus.PendingApproval,
            booking.Status);

        Assert.Null(booking.CancellationReason);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingAlreadyChangedState_ShouldNoOp()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking booking =
            BookingTestFactory
                .CreatePendingPaymentBooking();

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            new ExpireBookingApprovalCommandHandler(
                new StubBookingRepository(booking),
                unitOfWork,
                new FixedClock(UtcNow));

        Result result =
            await handler.HandleAsync(
                new ExpireBookingApprovalCommand(
                    booking.Id),
                cancellationToken);

        Assert.True(result.IsSuccess);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Null(
            booking.CancellationReason);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingDoesNotExist_ShouldReturnNotFound()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            new ExpireBookingApprovalCommandHandler(
                new StubBookingRepository(),
                unitOfWork,
                new FixedClock(UtcNow));

        Result result =
            await handler.HandleAsync(
                new ExpireBookingApprovalCommand(
                    bookingId),
                cancellationToken);

        Assert.True(result.IsFailure);

        Assert.Equal(
            ExpireBookingApprovalErrors.NotFound(
                bookingId),
            result.Error);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithNullCommand_ShouldThrow()
    {
        var handler =
            new ExpireBookingApprovalCommandHandler(
                new StubBookingRepository(),
                new SpyUnitOfWork(),
                new FixedClock(UtcNow));

        Task Action() =>
            handler.HandleAsync(null!);

        await Assert.ThrowsAsync<
            ArgumentNullException>(Action);
    }

    private sealed class StubBookingRepository
        : IBookingRepository
    {
        private readonly DomainBooking? _booking;

        public StubBookingRepository(
            DomainBooking? booking = null)
        {
            _booking = booking;
        }

        public Task<DomainBooking?> GetByIdAsync(
            Guid bookingId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                _booking?.Id == bookingId
                    ? _booking
                    : null);
        }

        public void Add(
            DomainBooking booking)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SpyUnitOfWork
        : IUnitOfWork
    {
        public int SaveChangesCallCount
        {
            get;
            private set;
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            SaveChangesCallCount++;

            return Task.CompletedTask;
        }
    }
}
