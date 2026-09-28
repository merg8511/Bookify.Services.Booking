namespace Bookify.Services.Booking.Domain.Tests.Infrastructure;

internal static class BookingTestTime
{
    public static readonly DateTimeOffset CreatedAtUtc = new(
        2026,
        9,
        1,
        12,
        0,
        0,
        TimeSpan.Zero);

    public static readonly DateTimeOffset ApprovalDueAtUtc = CreatedAtUtc.AddHours(24);
    public static readonly DateTimeOffset ApprovedAtUtc = CreatedAtUtc.AddHours(1);
    public static readonly DateTimeOffset PaymentDueAtUtc = ApprovedAtUtc.AddMinutes(30);
    public static readonly DateTimeOffset PaidAtUtc = CreatedAtUtc.AddHours(2);
    public static readonly DateTimeOffset CancelledAtUtc = CreatedAtUtc.AddHours(3);
    public static readonly DateTimeOffset CompletedAtUtc = CreatedAtUtc.AddHours(4);
}
