using Bookify.Services.Booking.Domain.Bookings.Events;
using Bookify.Services.Booking.Domain.Shared.DomainEvents;
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

        DateTimeOffset normalizedProcessedAtUtc = processedAtUtc.ToUniversalTime();

        if (normalizedProcessedAtUtc < CreatedAtUtc)
        {
            throw new InvalidOperationException("An outbox message cannot be processed before it was created.");
        }

        ProcessedAtUtc = normalizedProcessedAtUtc;
    }

}
