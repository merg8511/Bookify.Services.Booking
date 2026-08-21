using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.ExpirePayment;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Tests.Bookings.ExpirePayment;

public sealed class ExpireBookingPaymentCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenBookingIsPendingPayment_ShouldExpireAndSave()
    {
        // ARRANGE
        DomainBooking booking =
            CreatePendingPaymentBooking();

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            new ExpireBookingPaymentCommandHandler(
                new StubBookingRepository(
                    booking),
                unitOfWork);

        var command =
            new ExpireBookingPaymentCommand(
                booking.Id);

        // ACT
        Result result =
            await handler.HandleAsync(
                command);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            BookingStatus.Cancelled,
            booking.Status);

        Assert.Equal(
            BookingCancellationReason.PaymentExpired,
            booking.CancellationReason);

        Assert.False(
            booking.BlocksInventory);

        Assert.Equal(
            1,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingDoesNotExist_ShouldReturnNotFoundWithoutSaving()
    {
        // ARRANGE
        Guid bookingId =
            Guid.NewGuid();

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            new ExpireBookingPaymentCommandHandler(
                new StubBookingRepository(
                    null),
                unitOfWork);

        var command =
            new ExpireBookingPaymentCommand(
                bookingId);

        // ACT
        Result result =
            await handler.HandleAsync(
                command);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            ExpireBookingPaymentErrors.NotFound(
                bookingId),
            result.Error);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingIsPaid_ShouldReturnConflictWithoutSaving()
    {
        // ARRANGE
        DomainBooking booking =
            CreatePendingPaymentBooking();

        Result paidResult =
            booking.MarkAsPaid();

        Assert.True(
            paidResult.IsSuccess);

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            new ExpireBookingPaymentCommandHandler(
                new StubBookingRepository(
                    booking),
                unitOfWork);

        var command =
            new ExpireBookingPaymentCommand(
                booking.Id);

        // ACT
        Result result =
            await handler.HandleAsync(
                command);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            "Booking.InvalidStatusTransition",
            result.Error.Code);

        Assert.Equal(
            ErrorType.Conflict,
            result.Error.Type);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Assert.Null(
            booking.CancellationReason);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithNullCommand_ShouldThrow()
    {
        var handler =
            new ExpireBookingPaymentCommandHandler(
                new StubBookingRepository(
                    null),
                new SpyUnitOfWork());

        Task Action()
        {
            return handler.HandleAsync(
                null!);
        }

        await Assert.ThrowsAsync<
            ArgumentNullException>(Action);
    }

    private static DomainBooking CreatePendingPaymentBooking()
    {
        DomainBooking booking =
            CreateBooking();

        Result approvalResult =
            booking.Approve();

        Assert.True(
            approvalResult.IsSuccess);

        return booking;
    }

    private static DomainBooking CreateBooking()
    {
        RentableUnit rentableUnit =
            RentableUnit.Create(
                    Guid.NewGuid(),
                    "Room A",
                    RentableUnitType.Room,
                    maximumCapacity: 4,
                    maxBaseGuests: 2)
                .Value;

        StayPeriod stayPeriod =
            StayPeriod.Create(
                    new DateOnly(
                        2026,
                        9,
                        10),
                    new DateOnly(
                        2026,
                        9,
                        12))
                .Value;

        return DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                GuestCount.Create(2).Value)
            .Value;
    }

    private sealed class StubBookingRepository
        : IBookingRepository
    {
        private readonly DomainBooking? _booking;

        public StubBookingRepository(
            DomainBooking? booking)
        {
            _booking = booking;
        }

        public Task<DomainBooking?> GetByIdAsync(
            Guid bookingId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            DomainBooking? booking =
                _booking?.Id == bookingId
                    ? _booking
                    : null;

            return Task.FromResult(
                booking);
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
