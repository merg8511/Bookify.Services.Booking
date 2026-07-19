using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Domain.Tests.Bookings
{
    public sealed class BookingTests
    {
        private static readonly Guid PropertyId = Guid.NewGuid();

        [Fact]
        public void Create_WithValidData_ShouldReturnPendingApprovalBooking()
        {
            // ARRANGE
            var rentableUnit = CreateRentableUnit();
            var stayPeriod = CreateStayPeriod();

            // ACT
            var result = DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount: 2);

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.NotEqual(Guid.Empty, result.Value.Id);

            Assert.Equal(
                rentableUnit.PropertyId,
                result.Value.PropertyId);

            Assert.Equal(
                rentableUnit.Id,
                result.Value.RentableUnitId);

            Assert.Equal(
                stayPeriod,
                result.Value.StayPeriod);

            Assert.Equal(
                2,
                result.Value.GuestCount);

            Assert.Equal(
                BookingStatus.PendingApproval,
                result.Value.Status);

            Assert.Null(
                result.Value.CancellationReason);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-20)]
        public void Create_WithInvalidGuestCount_ShouldReturnFailure(int invalidGuestCount)
        {
            // ARRANGE
            var rentableUnit = CreateRentableUnit();
            var stayPeriod = CreateStayPeriod();

            // ACT
            var result = DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                invalidGuestCount);

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                BookingErrors.InvalidGuestCount,
                result.Error);
        }

        [Fact]
        public void Create_WhenGuestCountExceedsCapacity_ShouldReturnFailure()
        {
            // ARRANGE
            var rentableUnit = CreateRentableUnit(maximumCapacity: 4);
            var stayPeriod = CreateStayPeriod();

            // ACT
            var result = DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount: 5);

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                BookingErrors.GuestCapacityExceeded,
                result.Error);
        }

        [Fact]
        public void Create_WhenGuestCountEqualsCapacity_ShouldReturnSuccess()
        {
            // ARRANGE
            var rentableUnit = CreateRentableUnit(
                maximumCapacity: 4);
            var stayPeriod = CreateStayPeriod();

            // ACT
            var result = DomainBooking.Create(
               rentableUnit,
               stayPeriod,
               4);

            // ASSERT
            Assert.True(result.IsSuccess);
            Assert.Equal(
                4,
                result.Value.GuestCount);
        }

        [Fact]
        public void Create_WithInactiveRentableUnit_ShouldReturnFailure()
        {
            // ARRANGE
            var rentableUnit = CreateRentableUnit();
            rentableUnit.Deactivate();

            var stayPeriod = CreateStayPeriod();

            // ACT
            var result = DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount: 2);

            // ASSERT
            Assert.True(result.IsFailure);
            Assert.Equal(
                BookingErrors.RentableUnitInactive,
                result.Error);
        }

        [Fact]
        public void Create_TwiceWithSameData_ShouldCreateDifferentBookings()
        {
            // ARRANGE
            var rentableUnit = CreateRentableUnit();
            var stayPeriod = CreateStayPeriod();

            // ACT
            var firstResult = DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount: 2);

            var secondResult = DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                guestCount: 2);

            // ASSERT
            Assert.True(firstResult.IsSuccess);
            Assert.True(secondResult.IsSuccess);

            Assert.NotEqual(
                firstResult.Value.Id,
                secondResult.Value.Id);
        }

        [Fact]
        public void Approve_WhenPendingApproval_ShouldChangeStatusToPendingPayment()
        {
            // ARRANGE
            var booking = CreateBooking();

            // ACT
            var result = booking.Approve();

            // ASSERT
            Assert.True(result.IsSuccess);

            Assert.Equal(
                BookingStatus.PendingPayment,
                booking.Status);

            Assert.Null(
                booking.CancellationReason);
        }

        [Fact]
        public void Approve_WhenNotPendingApproval_ShouldReturnFailure()
        {
            // ARRANGE
            var booking = CreateBooking();
            booking.Approve();

            // ACT
            var result = booking.Approve();

            // ASSERT
            AssertInvalidTransition(result);

            Assert.Equal(
                BookingStatus.PendingPayment,
                booking.Status);
        }

        [Fact]
        public void Reject_WhenPendingApproval_ShouldCancelBooking()
        {
            // ARRANGE
            var booking = CreateBooking();

            // ACT
            var result = booking.Reject();

            // ASSERT
            Assert.True(result.IsSuccess);

            Assert.Equal(
                BookingStatus.Cancelled,
                booking.Status);

            Assert.Equal(
                BookingCancellationReason.RejectedByOwner,
                booking.CancellationReason);
        }

        [Fact]
        public void Reject_WhenPendingPayment_ShouldReturnFailure()
        {
            // ARRANGE
            var booking = CreateBooking();
            booking.Approve();

            // ACT
            var result = booking.Reject();

            // ASSERT
            AssertInvalidTransition(result);

            Assert.Equal(
                BookingStatus.PendingPayment,
                booking.Status);

            Assert.Null(booking.CancellationReason);
        }

        [Fact]
        public void MarkAsPaid_WhenPendingPayment_ShouldChangeStatusToPaid()
        {
            // ARRANGE
            var booking = CreateBooking();
            booking.Approve();

            // ACT
            var result = booking.MarkAsPaid();

            // ASSERT
            Assert.True(result.IsSuccess);

            Assert.Equal(
                BookingStatus.Paid,
                booking.Status);

            Assert.Null(
                booking.CancellationReason);
        }

        [Fact]
        public void MarkAsPaid_WhenPendingApproval_ShouldReturnFailure()
        {
            // ARRANGE
            var booking = CreateBooking();

            // ACT
            var result = booking.MarkAsPaid();

            // ASSERT
            AssertInvalidTransition(result);

            Assert.Equal(
                BookingStatus.PendingApproval,
                booking.Status);
        }

        [Fact]
        public void ExpirePayment_WhenPendingPayment_ShouldCancelBooking()
        {
            // ARRANGE
            var booking = CreateBooking();
            booking.Approve();

            // ACT
            var result = booking.ExpirePayment();

            // ASSERT
            Assert.True(result.IsSuccess);

            Assert.Equal(
                BookingStatus.Cancelled,
                booking.Status);

            Assert.Equal(
                BookingCancellationReason.PaymentExpired,
                booking.CancellationReason);
        }

        [Fact]
        public void ExpirePayment_WhenPendingApprobal_ShouldReturnFailure()
        {
            // ARRANGE
            var booking = CreateBooking();

            // ACT
            var result = booking.ExpirePayment();

            // ASSERT
            AssertInvalidTransition(result);

            Assert.Equal(
                BookingStatus.PendingApproval,
                booking.Status);

            Assert.Null(
                booking.CancellationReason);
        }

        [Fact]
        public void Complete_WhenPaid_ShouldChangeStatusToCompleted()
        {
            // ARRANGE
            var booking = CreatePaidBooking();

            // ACT
            var result = booking.Complete();

            // ASSERT
            Assert.True(result.IsSuccess);

            Assert.Equal(
                BookingStatus.Completed,
                booking.Status);
        }

        [Fact]
        public void Complete_WhenPendingPayment_ShouldReturnFailure()
        {
            // ARRANGE
            var booking = CreateBooking();
            booking.Approve();

            // ACT
            var result = booking.Complete();

            // ASSERT
            AssertInvalidTransition(result);

            Assert.Equal(
                BookingStatus.PendingPayment,
                booking.Status);
        }

        [Fact]
        public void ValidLifecycle_ShouldReachCompletedStatus()
        {
            // ARRANGE
            var booking = CreateBooking();

            // ACT
            var approvalResult = booking.Approve();
            var paymentResult = booking.MarkAsPaid();
            var completionResult = booking.Complete();

            // ASSERT
            Assert.True(approvalResult.IsSuccess);
            Assert.True(paymentResult.IsSuccess);
            Assert.True(completionResult.IsSuccess);

            Assert.Equal(
                BookingStatus.Completed,
                booking.Status);

            Assert.Null(booking.CancellationReason);
        }

        [Fact]
        public void CancelledBooking_ShouldNotAllowPayment()
        {
            // ARRANGE
            var booking = CreateBooking();
            booking.Approve();
            booking.ExpirePayment();

            // ACT
            var result = booking.MarkAsPaid();

            // ASSERT
            AssertInvalidTransition(result);

            Assert.Equal(
                BookingStatus.Cancelled,
                booking.Status);

            Assert.Equal(
                BookingCancellationReason.PaymentExpired,
                booking.CancellationReason);
        }

        [Fact]
        public void CompletedBooking_ShouldNotAllowAnotherTransition()
        {
            // ARRANGE
            var booking = CreatePaidBooking();
            booking.Complete();

            // ACT
            var result = booking.Complete();

            // ASSERT
            AssertInvalidTransition(result);

            Assert.Equal(
                BookingStatus.Completed,
                booking.Status);
        }

        [Fact]
        public void Create_WithNullRentaableUnit_ShouldThrow()
        {
            // ARRANGE
            var stayPeriod = CreateStayPeriod();

            // ACT
            void Action()
            {
                DomainBooking.Create(
                    null!,
                    stayPeriod,
                    guestCount: 2);
            }

            // ASSERT
            Assert.Throws<ArgumentNullException>(Action);
        }

        [Fact]
        public void Create_WithNullStayPeriod_ShouldThrow()
        {
            // ARRANGE
            var rentableUnit = CreateRentableUnit();

            // ACT
            void Action()
            {
                DomainBooking.Create(
                    rentableUnit,
                    null!,
                    guestCount: 2);
            }

            // ASSERT
            Assert.Throws<ArgumentNullException>(Action);
        }

        private static void AssertInvalidTransition(
            Result result)
        {
            Assert.True(result.IsFailure);

            Assert.Equal(
                "Booking.InvalidStatusTransition",
                result.Error.Code);

            Assert.Equal(
                ErrorType.Conflict,
                result.Error.Type);
        }

        private static DomainBooking CreateBooking()
        {
            return DomainBooking.Create(
                CreateRentableUnit(),
                CreateStayPeriod(),
                guestCount: 2).Value;
        }

        private static DomainBooking CreatePaidBooking()
        {
            var booking = CreateBooking();

            booking.Approve();
            booking.MarkAsPaid();

            return booking;
        }

        private static RentableUnit CreateRentableUnit(int maximumCapacity = 4)
        {
            return RentableUnit.Create(
                PropertyId,
                "Habitación principal",
                RentableUnitType.Room,
                maximumCapacity,
                maxBaseGuests: 2).Value;
        }

        private static StayPeriod CreateStayPeriod()
        {
            return StayPeriod.Create(
                new DateOnly(2026, 7, 10),
                new DateOnly(2026, 7, 12)).Value;
        }
    }
}
