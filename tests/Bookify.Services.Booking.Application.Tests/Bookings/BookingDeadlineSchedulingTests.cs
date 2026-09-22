using Bookify.Services.Booking.Application.Tests.Infrastructure;
using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Domain.Tests.Bookings;

public sealed class BookingDeadlineSchedulingTests
{
    private static readonly DateTimeOffset
        ApprovalDueAtUtc =
            BookingTestTime.CreatedAtUtc
                .AddHours(24);

    private static readonly DateTimeOffset
        PaymentDueAtUtc =
            BookingTestTime.ApprovedAtUtc
                .AddMinutes(30);

    [Fact]
    public void ScheduleApprovalDeadline_ShouldPersistDeadline()
    {
        DomainBooking booking =
            CreateBooking();

        Result result =
            booking.ScheduleApprovalDeadline(
                ApprovalDueAtUtc);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            ApprovalDueAtUtc,
            booking.ApprovalDueAtUtc);
    }

    [Fact]
    public void ScheduleApprovalDeadline_WhenDeadlineIsNotAfterCreation_ShouldFail()
    {
        DomainBooking booking =
            CreateBooking();

        Result result =
            booking.ScheduleApprovalDeadline(
                BookingTestTime.CreatedAtUtc);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            BookingDeadlineErrors.InvalidApprovalDeadline,
            result.Error);

        Assert.Null(
            booking.ApprovalDueAtUtc);
    }

    [Fact]
    public void ScheduleApprovalDeadline_WhenAlreadyScheduled_ShouldFail()
    {
        DomainBooking booking =
            CreateBooking();

        Result first =
            booking.ScheduleApprovalDeadline(
                ApprovalDueAtUtc);

        Assert.True(
            first.IsSuccess);

        Result second =
            booking.ScheduleApprovalDeadline(
                ApprovalDueAtUtc.AddHours(1));

        Assert.True(
            second.IsFailure);

        Assert.Equal(
            BookingDeadlineErrors
                .ApprovalDeadlineAlreadyScheduled,
            second.Error);

        Assert.Equal(
            ApprovalDueAtUtc,
            booking.ApprovalDueAtUtc);
    }

    [Fact]
    public void SchedulePaymentDeadline_AfterApproval_ShouldPersistDeadline()
    {
        DomainBooking booking =
            CreateBooking();

        Result approvalResult =
            booking.Approve(
                BookingTestTime.ApprovedAtUtc);

        Assert.True(
            approvalResult.IsSuccess);

        Result result =
            booking.SchedulePaymentDeadline(
                PaymentDueAtUtc);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            PaymentDueAtUtc,
            booking.PaymentDueAtUtc);
    }

    [Fact]
    public void SchedulePaymentDeadline_BeforeApproval_ShouldFail()
    {
        DomainBooking booking =
            CreateBooking();

        Result result =
            booking.SchedulePaymentDeadline(
                PaymentDueAtUtc);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            "Booking.InvalidStatusForPaymentDeadline",
            result.Error.Code);

        Assert.Null(
            booking.PaymentDueAtUtc);
    }

    [Fact]
    public void SchedulePaymentDeadline_WhenDeadlineIsNotAfterApproval_ShouldFail()
    {
        DomainBooking booking =
            CreateBooking();

        Result approvalResult =
            booking.Approve(
                BookingTestTime.ApprovedAtUtc);

        Assert.True(
            approvalResult.IsSuccess);

        Result result =
            booking.SchedulePaymentDeadline(
                BookingTestTime.ApprovedAtUtc);

        Assert.True(
            result.IsFailure);

        Assert.Equal(
            BookingDeadlineErrors.InvalidPaymentDeadline,
            result.Error);

        Assert.Null(
            booking.PaymentDueAtUtc);
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
                        10,
                        10),
                    new DateOnly(
                        2026,
                        10,
                        12))
                .Value;

        GuestDetails guestDetails =
            GuestDetails.Create(
                    "John Doe",
                    "john@example.com",
                    "+50377778888")
                .Value;

        Result<DomainBooking> result =
            DomainBooking.Create(
                rentableUnit,
                stayPeriod,
                GuestCount.Create(2).Value,
                guestDetails,
                BookingTestTime.CreatedAtUtc);

        Assert.True(
            result.IsSuccess);

        return result.Value;
    }
}
