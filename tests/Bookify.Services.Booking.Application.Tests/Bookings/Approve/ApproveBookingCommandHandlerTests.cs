using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.Approve;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Approve;

public sealed class ApproveBookingCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenBookingIsPendingApproval_ShouldApproveAndSave()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreateBooking();

        var bookingRepository =
            new StubBookingRepository(
                booking);

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            new ApproveBookingCommandHandler(
                bookingRepository,
                unitOfWork);

        var command =
            new ApproveBookingCommand(
                booking.Id);

        // ACT
        Result result =
            await handler.HandleAsync(
                command,
                cancellationToken);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Null(
            booking.CancellationReason);

        Assert.Equal(
            1,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingDoesNotExist_ShouldReturnNotFoundWithoutSaving()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        var bookingRepository =
            new StubBookingRepository(
                null);

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            new ApproveBookingCommandHandler(
                bookingRepository,
                unitOfWork);

        var command =
            new ApproveBookingCommand(
                bookingId);

        // ACT
        Result result =
            await handler.HandleAsync(
                command,
                cancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            ApproveBookingErrors.NotFound(
                bookingId),
            result.Error);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingIsNotPendingApproval_ShouldReturnConflictWithoutSaving()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking =
            CreateBooking();

        booking.Approve();

        var bookingRepository =
            new StubBookingRepository(
                booking);

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            new ApproveBookingCommandHandler(
                bookingRepository,
                unitOfWork);

        var command =
            new ApproveBookingCommand(
                booking.Id);

        // ACT
        Result result =
            await handler.HandleAsync(
                command,
                cancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            "Booking.InvalidStatusTransition",
            result.Error.Code);

        Assert.Equal(
            ErrorType.Conflict,
            result.Error.Type);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithNullCommand_ShouldThrow()
    {
        // ARRANGE
        var handler =
            new ApproveBookingCommandHandler(
                new StubBookingRepository(
                    null),
                new SpyUnitOfWork());

        // ACT
        Task Action()
        {
            return handler.HandleAsync(
                null!);
        }

        // ASSERT
        await Assert.ThrowsAsync<
            ArgumentNullException>(Action);
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
                    8,
                    20),
                new DateOnly(
                    2026,
                    8,
                    22))
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
