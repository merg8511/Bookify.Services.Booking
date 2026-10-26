namespace Bookify.Services.Booking.Api.Endpoints.Administrative.Bookings.Payment;

public sealed record GetAdminBookingPaymentResponse(
    Guid PropertyId,
    Guid BookingId,
    string BookingStatus,
    AdminPaymentDetailsResponse? Payment);

public sealed record AdminPaymentDetailsResponse(
    Guid Id,
    string Status,
    decimal Amount,
    string Currency,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool HasPendingInitiation,
    DateTimeOffset? PendingInitiationCreatedAtUtc,
    IReadOnlyList<AdminPaymentAttemptResponse> Attempts);

public sealed record AdminPaymentAttemptResponse(
    Guid Id,
    string Status,
    string ExternalReference,
    decimal Amount,
    string Currency,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc);
