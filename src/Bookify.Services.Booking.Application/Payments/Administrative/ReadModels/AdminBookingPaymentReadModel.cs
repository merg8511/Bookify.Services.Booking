namespace Bookify.Services.Booking.Application.Payments.Administrative.ReadModels;

public sealed record AdminBookingPaymentReadModel(
    Guid BookingId,
    string BookingStatus,
    AdminPaymentReadModel? Payment);

public sealed record AdminPaymentReadModel(
    Guid Id,
    string Status,
    decimal Amount,
    string Currency,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool HasPendingInitiation,
    DateTimeOffset? PendingInitiationCreatedAtUtc,
    IReadOnlyList<AdminPaymentAttemptReadModel> Attempts);

public sealed record AdminPaymentAttemptReadModel(
    Guid Id,
    string Status,
    string ExternalReference,
    decimal Amount,
    string Currency,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc);
