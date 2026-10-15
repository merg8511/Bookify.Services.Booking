using Bookify.Services.Booking.Application.Outbox;
using Bookify.Services.Booking.Domain.Bookings.Events;
using Bookify.Services.Booking.Domain.Shared.DomainEvents;
using MimeKit.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Outbox;

internal sealed class OutboxMessage
{
    private static readonly JsonSerializerOptions SerializationOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private OutboxMessage()
    {
        EventType = string.Empty;
        Payload = string.Empty;
    }

    private OutboxMessage(
        Guid id,
        string eventType,
        string payload,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        EventType = eventType;
        Payload = payload;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string EventType { get; private set; }
    public string Payload { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset? NextAttemptAtUtc { get; private set; }
    public DateTimeOffset? FailedAtUtc { get; private set; }
    public string? LastFailureCode { get; private set; }

    public static OutboxMessage FromDomainEvent(
        IDomainEvent domainEvent,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        string eventType = domainEvent switch
        {
            BookingCreatedDomainEvent => nameof(BookingCreatedDomainEvent),
            BookingApprovedDomainEvent => nameof(BookingApprovedDomainEvent),
            BookingPaidDomainEvent => nameof(BookingPaidDomainEvent),
            BookingCancelledDomainEvent => nameof(BookingCancelledDomainEvent),
            _ => throw new NotSupportedException($"The domain event '{domainEvent.GetType().Name}' " +
                $"does not have an Outbox mapping.")
        };

        string payload = JsonSerializer.Serialize(
            domainEvent,
            domainEvent.GetType(),
            SerializationOptions);

        return new OutboxMessage(
            Guid.NewGuid(),
            eventType,
            payload,
            createdAtUtc.ToUniversalTime());
    }

    public void MarkProcessed(DateTimeOffset processedAtUtc)
    {
        if (ProcessedAtUtc is not null)
        {
            return;
        }

        EnsureNotDeadLettered();

        DateTimeOffset normalizedProcessedAtUtc = processedAtUtc.ToUniversalTime();

        if (normalizedProcessedAtUtc < CreatedAtUtc)
        {
            throw new InvalidOperationException("An outbox message cannot be processed before it was created.");
        }

        ProcessedAtUtc = normalizedProcessedAtUtc;
        NextAttemptAtUtc = null;
        LastFailureCode = null;
    }

    public OutboxFailureOutcome RecordFailure(
        DateTimeOffset failedAtUtc,
        string failureCode,
        int maxAttempts,
        TimeSpan baseDelay,
        TimeSpan maxDelay,
        bool isPermanent)
    {
        EnsurePending();

        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);

        if (failureCode.Length > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(failureCode));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);

        if (baseDelay <= TimeSpan.Zero || maxDelay < baseDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(baseDelay));
        }

        DateTimeOffset now = failedAtUtc.ToUniversalTime();
        AttemptCount++;
        LastFailureCode = failureCode;

        if (isPermanent || AttemptCount >= maxAttempts)
        {
            FailedAtUtc = now;
            NextAttemptAtUtc = null;

            return new OutboxFailureOutcome(
                AttemptCount,
                IsDeadLettered: true,
                NextAttemptAtUtc: null);
        }

        double delayMilliseconds = Math.Min(
            maxDelay.TotalMilliseconds,
            baseDelay.TotalMilliseconds * Math.Pow(2, Math.Min(AttemptCount - 1, 20)));

        NextAttemptAtUtc = now.Add(TimeSpan.FromMilliseconds(delayMilliseconds));

        return new OutboxFailureOutcome(
            AttemptCount,
            IsDeadLettered: false,
            NextAttemptAtUtc);
    }

    public void Defer(DateTimeOffset nextAttemptAtUtc)
    {
        EnsurePending();
        NextAttemptAtUtc = nextAttemptAtUtc.ToUniversalTime();
    }

    private void EnsurePending()
    {
        if (ProcessedAtUtc is not null)
        {
            throw new InvalidOperationException(
                "A processed outbox message cannot be retried.");
        }

        EnsureNotDeadLettered();
    }

    private void EnsureNotDeadLettered()
    {
        if (FailedAtUtc is not null)
        {
            throw new InvalidOperationException(
                "A dead-lettered outbox message cannot be processed automatically.");
        }
    }
}
