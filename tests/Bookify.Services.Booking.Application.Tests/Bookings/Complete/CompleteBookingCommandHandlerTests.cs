using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings.Complete;
using Bookify.Services.Booking.Application.Tests.Infrastructure;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Complete;

public sealed class CompleteBookingCommandHandlerTests
{
    private static readonly DateOnly CheckOutDate =
        new(2026, 9, 10);

    // America/El_Salvador = UTC-6.
    // Local checkout 11:00 corresponds to 17:00 UTC.
    private static readonly DateTimeOffset CheckoutAtUtc =
        new(
            2026,
            9,
            10,
            17,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_WhenPaidBookingReachedCheckout_ShouldCompleteAndSave()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        (Property property, DomainBooking booking) =
            CreatePaidBooking();

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            new CompleteBookingCommandHandler(
                new StubBookingRepository(
                    booking),
                new StubPropertyRepository(
                    property),
                unitOfWork,
                new FixedClock(
                    CheckoutAtUtc));

        var command =
            new CompleteBookingCommand(
                booking.Id);

        // ACT
        Result result =
            await handler.HandleAsync(
                command,
                cancellationToken);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            BookingStatus.Completed,
            booking.Status);

        Assert.Null(
            booking.CancellationReason);

        Assert.True(
            booking.BlocksInventory);

        Assert.Equal(
            CheckoutAtUtc,
            booking.CompletedAtUtc);

        Assert.Equal(
            1,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenPaidBookingHasNotReachedCheckout_ShouldNoOp()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        (Property property, DomainBooking booking) =
            CreatePaidBooking();

        DateTimeOffset beforeCheckout =
            CheckoutAtUtc.AddMinutes(-1);

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            new CompleteBookingCommandHandler(
                new StubBookingRepository(
                    booking),
                new StubPropertyRepository(
                    property),
                unitOfWork,
                new FixedClock(
                    beforeCheckout));

        // ACT
        Result result =
            await handler.HandleAsync(
                new CompleteBookingCommand(
                    booking.Id),
                cancellationToken);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Assert.Null(
            booking.CompletedAtUtc);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingAlreadyCompleted_ShouldNoOp()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        (Property property, DomainBooking booking) =
            CreatePaidBooking();

        Assert.True(
            booking.Complete(
                CheckoutAtUtc)
                .IsSuccess);

        DateTimeOffset originalCompletedAtUtc =
            booking.CompletedAtUtc!.Value;

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            new CompleteBookingCommandHandler(
                new StubBookingRepository(
                    booking),
                new StubPropertyRepository(
                    property),
                unitOfWork,
                new FixedClock(
                    CheckoutAtUtc.AddHours(1)));

        // ACT
        Result result =
            await handler.HandleAsync(
                new CompleteBookingCommand(
                    booking.Id),
                cancellationToken);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            BookingStatus.Completed,
            booking.Status);

        Assert.Equal(
            originalCompletedAtUtc,
            booking.CompletedAtUtc);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingIsNotPaid_ShouldNoOp()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Property property =
            PropertyTestFactory.CreateValidProperty();

        RentableUnit rentableUnit =
            RentableUnitTestFactory
                .CreateValidRentableUnit(
                    property.Id);

        StayPeriod stayPeriod =
            StayPeriod.Create(
                CheckOutDate.AddDays(-2),
                CheckOutDate)
            .Value;

        DomainBooking booking =
            BookingTestFactory
                .CreatePendingPaymentBooking(
                    rentableUnit:
                        rentableUnit,
                    stayPeriod:
                        stayPeriod);

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            new CompleteBookingCommandHandler(
                new StubBookingRepository(
                    booking),
                new StubPropertyRepository(
                    property),
                unitOfWork,
                new FixedClock(
                    CheckoutAtUtc.AddHours(1)));

        // ACT
        Result result =
            await handler.HandleAsync(
                new CompleteBookingCommand(
                    booking.Id),
                cancellationToken);

        // ASSERT
        Assert.True(result.IsSuccess);

        Assert.Equal(
            BookingStatus.PendingPayment,
            booking.Status);

        Assert.Null(
            booking.CompletedAtUtc);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenBookingDoesNotExist_ShouldReturnNotFoundWithoutSaving()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            new CompleteBookingCommandHandler(
                new StubBookingRepository(),
                new StubPropertyRepository(),
                unitOfWork,
                new FixedClock(
                    CheckoutAtUtc));

        // ACT
        Result result =
            await handler.HandleAsync(
                new CompleteBookingCommand(
                    bookingId),
                cancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            CompleteBookingErrors.NotFound(
                bookingId),
            result.Error);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenPropertyDoesNotExist_ShouldReturnFailureWithoutCompleting()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        (_, DomainBooking booking) =
            CreatePaidBooking();

        var unitOfWork =
            new SpyUnitOfWork();

        var handler =
            new CompleteBookingCommandHandler(
                new StubBookingRepository(
                    booking),
                new StubPropertyRepository(),
                unitOfWork,
                new FixedClock(
                    CheckoutAtUtc));

        // ACT
        Result result =
            await handler.HandleAsync(
                new CompleteBookingCommand(
                    booking.Id),
                cancellationToken);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            CompleteBookingErrors
                .PropertyNotFound(
                    booking.PropertyId),
            result.Error);

        Assert.Equal(
            BookingStatus.Paid,
            booking.Status);

        Assert.Null(
            booking.CompletedAtUtc);

        Assert.Equal(
            0,
            unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAsync_WithNullCommand_ShouldThrow()
    {
        var handler =
            new CompleteBookingCommandHandler(
                new StubBookingRepository(),
                new StubPropertyRepository(),
                new SpyUnitOfWork(),
                new FixedClock(
                    CheckoutAtUtc));

        Task Action() =>
            handler.HandleAsync(null!);

        await Assert.ThrowsAsync<
            ArgumentNullException>(Action);
    }

    private static (
        Property Property,
        DomainBooking Booking)
        CreatePaidBooking()
    {
        Property property =
            PropertyTestFactory.CreateValidProperty(
                timeZoneId:
                    "America/El_Salvador",
                checkOutTime:
                    new TimeOnly(11, 0));

        RentableUnit rentableUnit =
            RentableUnitTestFactory
                .CreateValidRentableUnit(
                    property.Id);

        StayPeriod stayPeriod =
            StayPeriod.Create(
                CheckOutDate.AddDays(-2),
                CheckOutDate)
            .Value;

        DomainBooking booking =
            BookingTestFactory.CreatePaidBooking(
                rentableUnit:
                    rentableUnit,
                stayPeriod:
                    stayPeriod);

        return (
            property,
            booking);
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

    private sealed class StubPropertyRepository
        : IPropertyRepository
    {
        private readonly Property? _property;

        public StubPropertyRepository(
            Property? property = null)
        {
            _property = property;
        }

        public Task<Property?> GetByIdAsync(
            Guid propertyId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                _property?.Id == propertyId
                    ? _property
                    : null);
        }

        public void Add(
            Property property)
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
