namespace Bookify.Services.Booking.Application.Outbox;

public sealed record OutboxFailureOutcome(
    int AttemptCount,
    bool IsDeadLettered,
    DateTimeOffset? NextAttemptAtUtc);
