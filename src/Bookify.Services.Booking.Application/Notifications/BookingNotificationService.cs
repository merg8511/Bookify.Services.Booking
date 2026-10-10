using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Shared;
using System.Globalization;

namespace Bookify.Services.Booking.Application.Notifications;

public sealed class BookingNotificationService
{
    private readonly IBookingReadService _bookingReadService;
    private readonly IBookingNotificationSender _notificationSender;
    private readonly INotificationDeliveryStore _deliveryStore;
    private readonly IClock _clock;

    public BookingNotificationService(
        IBookingReadService bookingReadService,
        IBookingNotificationSender notificationSender,
        INotificationDeliveryStore deliveryStore,
        IClock clock)
    {
        _bookingReadService = bookingReadService ??
            throw new ArgumentNullException(nameof(bookingReadService));

        _notificationSender = notificationSender ??
            throw new ArgumentNullException(nameof(notificationSender));

        _deliveryStore = deliveryStore ??
            throw new ArgumentNullException(nameof(deliveryStore));

        _clock = clock ??
            throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<BookingNotificationDispatchOutcome>> SendAsync(
        Guid bookingId,
        BookingNotificationType notificationType,
        CancellationToken cancellationToken = default)
    {
        if (bookingId == Guid.Empty)
        {
            return Result<BookingNotificationDispatchOutcome>.Failure(
                BookingNotificationErrors.InvalidBookingId);
        }

        bool alreadyDelivered = await _deliveryStore.HasBeenDeliveredAsync(
            bookingId, notificationType, cancellationToken);

        if (alreadyDelivered)
        {
            return Result<BookingNotificationDispatchOutcome>.Success(
                BookingNotificationDispatchOutcome.AlreadyDelivered);
        }

        BookingDetailsReadModel? booking = await _bookingReadService
            .GetByIdAsync(bookingId, cancellationToken);

        if (booking is null)
        {
            return Result<BookingNotificationDispatchOutcome>.Failure(
                BookingNotificationErrors.BookingNotFound(bookingId));
        }

        if (ShouldSkip(booking, notificationType))
        {
            return Result<BookingNotificationDispatchOutcome>.Success(
                BookingNotificationDispatchOutcome.Skipped);
        }

        if (string.IsNullOrWhiteSpace(booking.GuestEmail))
        {
            return Result<BookingNotificationDispatchOutcome>.Failure(
                BookingNotificationErrors.RecipientUnavailable(booking.Id));
        }

        Result<BookingNotificationMessage> messageResult = CreateMessage(booking, notificationType);

        if (messageResult.IsFailure)
        {
            return Result<BookingNotificationDispatchOutcome>.Failure(messageResult.Error);
        }

        Result deliveryResult = await _notificationSender.SendAsync(messageResult.Value, cancellationToken);

        if (deliveryResult.IsFailure)
        {
            return Result<BookingNotificationDispatchOutcome>.Failure(deliveryResult.Error);
        }

        await _deliveryStore.TryRecordDeliveredAsync(
            booking.Id,
            notificationType,
            _clock.UtcNow,
            cancellationToken);

        return Result<BookingNotificationDispatchOutcome>.Success(BookingNotificationDispatchOutcome.Sent);
    }

    private static bool ShouldSkip(
        BookingDetailsReadModel booking,
        BookingNotificationType notificationType)
    {
        return notificationType switch
        {
            BookingNotificationType.PaymentDeadlineReminder =>
                !string.Equals(booking.Status, nameof(BookingStatus.PendingPayment), StringComparison.Ordinal),

            BookingNotificationType.UpcomingCheckInReminder =>
                !string.Equals(booking.Status, nameof(BookingStatus.Paid), StringComparison.Ordinal),

            _ => false
        };
    }

    private static Result<BookingNotificationMessage> CreateMessage(
        BookingDetailsReadModel booking,
        BookingNotificationType notificationType)
    {
        return notificationType switch
        {
            BookingNotificationType.BookingApproved => CreateApprovedMessage(booking),

            BookingNotificationType.PaymentConfirmed => Result<BookingNotificationMessage>
                .Success(CreatePaymentConfirmedMessage(booking)),

            BookingNotificationType.BookingCancelled => Result<BookingNotificationMessage>
                .Success(CreateCancelledMessage(booking)),

            BookingNotificationType.PaymentDeadlineReminder => CreatePaymentDeadlineReminder(booking),

            BookingNotificationType.UpcomingCheckInReminder => Result<BookingNotificationMessage>
                .Success(CreateUpcomingCheckInReminder(booking)),

            _ => throw new InvalidOperationException(
                $"Unsupported booking notification type '{notificationType}'.")
        };
    }

    private static Result<BookingNotificationMessage> CreateApprovedMessage(
        BookingDetailsReadModel booking)
    {
        if (booking.PaymentDueAtUtc is null)
        {
            return Result<BookingNotificationMessage>.Failure(
                BookingNotificationErrors.RequiredDataMissing(booking.Id, nameof(booking.PaymentDueAtUtc)));
        }

        string body =
            $"""
            {GetGreeting(booking)}

            Your booking {booking.BookingReference} at {booking.PropertyName} has been approved.

            Stay: {FormatDate(booking.CheckInDate)} to {FormatDate(booking.CheckOutDate)}.
            Payment deadline: {FormatUtc(booking.PaymentDueAtUtc.Value)}.

            Thank you,
            Bookify
            """;

        return Result<BookingNotificationMessage>.Success(
            new BookingNotificationMessage(
                booking.Id,
                BookingNotificationType.BookingApproved,
                booking.GuestEmail!,
                $"Booking {booking.BookingReference} approved",
                body));
    }

    private static BookingNotificationMessage CreatePaymentConfirmedMessage(
        BookingDetailsReadModel booking)
    {
        string body =
            $"""
            {GetGreeting(booking)}

            Payment for booking {booking.BookingReference} has been confirmed.

            Property: {booking.PropertyName}
            Stay: {FormatDate(booking.CheckInDate)} to {FormatDate(booking.CheckOutDate)}.

            Thank you,
            Bookify
            """;

        return new BookingNotificationMessage(
            booking.Id,
            BookingNotificationType.PaymentConfirmed,
            booking.GuestEmail!,
            $"Payment confirmed for booking {booking.BookingReference}",
            body);
    }

    private static BookingNotificationMessage CreateCancelledMessage(
        BookingDetailsReadModel booking)
    {
        string reason = GetCancellationReasonText(booking.CancellationReason);

        string body =
            $"""
            {GetGreeting(booking)}

            Booking {booking.BookingReference} at {booking.PropertyName} has been cancelled.

            Reason: {reason}.

            Thank you,
            Bookify
            """;

        return new BookingNotificationMessage(
            booking.Id,
            BookingNotificationType.BookingCancelled,
            booking.GuestEmail!,
            $"Booking {booking.BookingReference} cancelled",
            body);
    }

    private static Result<BookingNotificationMessage> CreatePaymentDeadlineReminder(
        BookingDetailsReadModel booking)
    {
        if (booking.PaymentDueAtUtc is null)
        {
            return Result<BookingNotificationMessage>.Failure(
                BookingNotificationErrors.RequiredDataMissing(booking.Id, nameof(booking.PaymentDueAtUtc)));
        }

        string body =
            $"""
            {GetGreeting(booking)}

            Payment for booking {booking.BookingReference} is still pending.

            Payment deadline: {FormatUtc(booking.PaymentDueAtUtc.Value)}.

            Complete the payment before the deadline to keep your booking active.

            Thank you,
            Bookify
            """;

        return Result<BookingNotificationMessage>.Success(
            new BookingNotificationMessage(
                booking.Id,
                BookingNotificationType.PaymentDeadlineReminder,
                booking.GuestEmail!,
                $"Payment reminder for booking {booking.BookingReference}",
                body));
    }

    private static BookingNotificationMessage CreateUpcomingCheckInReminder(
        BookingDetailsReadModel booking)
    {
        string body =
            $"""
            {GetGreeting(booking)}

            Your stay at {booking.PropertyName} is approaching.

            Booking: {booking.BookingReference}
            Check-in date: {FormatDate(booking.CheckInDate)}.

            We look forward to your stay.

            Bookify
            """;

        return new BookingNotificationMessage(
            booking.Id,
            BookingNotificationType.UpcomingCheckInReminder,
            booking.GuestEmail!,
            $"Upcoming check-in for booking {booking.BookingReference}",
            body);
    }

    private static string GetGreeting(BookingDetailsReadModel booking)
    {
        return string.IsNullOrWhiteSpace(booking.GuestFullName) ? "Hello," : $"Hello {booking.GuestFullName},";
    }

    private static string GetCancellationReasonText(string? cancellationReason)
    {
        return cancellationReason switch
        {
            nameof(BookingCancellationReason.RejectedByOwner) => "the booking was rejected by the property owner",
            nameof(BookingCancellationReason.PaymentExpired) => "the payment deadline expired",
            nameof(BookingCancellationReason.CancelledByGuest) => "th booking was cancelled by the guest",
            nameof(BookingCancellationReason.ApprovalExpired) => "the approval deadline expired",
            _ => "the booking was cancelled"
        };
    }

    private static string FormatDate(DateOnly date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string FormatUtc(DateTimeOffset datetime)
    {
        return datetime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
    }
}
