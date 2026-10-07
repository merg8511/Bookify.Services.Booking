namespace Bookify.Services.Booking.Application.Outbox;

public class PendingOutboxMessageReadModel
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
