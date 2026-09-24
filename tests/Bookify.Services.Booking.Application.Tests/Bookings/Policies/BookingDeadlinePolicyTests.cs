using Bookify.Services.Booking.Application.Bookings.Policies;

namespace Bookify.Services.Booking.Application.Tests.Bookings.Policies;

public sealed class BookingDeadlinePolicyTests
{
    [Fact]
    public void GetApprovalDueAtUtc_ShouldAddApprovalWindow()
    {
        // Arrange
        DateTimeOffset createdAtUtc =
            new(
                2026,
                9,
                21,
                18,
                0,
                0,
                TimeSpan.Zero);

        var policy =
            new BookingDeadlinePolicy(
                TimeSpan.FromHours(24),
                TimeSpan.FromMinutes(30));

        // Act
        DateTimeOffset result =
            policy.GetApprovalDueAtUtc(
                createdAtUtc);

        // Assert
        Assert.Equal(
            createdAtUtc.AddHours(24),
            result);
    }

    [Fact]
    public void GetPaymentDueAtUtc_ShouldAddPaymentWindow()
    {
        // Arrange
        DateTimeOffset approvedAtUtc =
            new(
                2026,
                9,
                21,
                18,
                0,
                0,
                TimeSpan.Zero);

        var policy =
            new BookingDeadlinePolicy(
                TimeSpan.FromHours(24),
                TimeSpan.FromMinutes(30));

        // Act
        DateTimeOffset result =
            policy.GetPaymentDueAtUtc(
                approvedAtUtc);

        // Assert
        Assert.Equal(
            approvedAtUtc.AddMinutes(30),
            result);
    }

    [Fact]
    public void Constructor_WithZeroApprovalWindow_ShouldThrow()
    {
        void Action()
        {
            _ = new BookingDeadlinePolicy(
                TimeSpan.Zero,
                TimeSpan.FromMinutes(30));
        }

        Assert.Throws<
            ArgumentOutOfRangeException>(
                Action);
    }

    [Fact]
    public void Constructor_WithZeroPaymentWindow_ShouldThrow()
    {
        void Action()
        {
            _ = new BookingDeadlinePolicy(
                TimeSpan.FromHours(24),
                TimeSpan.Zero);
        }

        Assert.Throws<
            ArgumentOutOfRangeException>(
                Action);
    }
}
