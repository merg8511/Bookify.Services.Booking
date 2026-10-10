using Bookify.Services.Booking.Application.Abstractions.DomainEvents;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Domain.Payments;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared.DomainEvents;
using Bookify.Services.Booking.Infrastructure.Persistence.Idempotency;
using Bookify.Services.Booking.Infrastructure.Persistence.Notifications;
using Bookify.Services.Booking.Infrastructure.Persistence.Outbox;
using Bookify.Services.Booking.Infrastructure.Persistence.Payments.Webhooks;
using Microsoft.EntityFrameworkCore;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Infrastructure.Persistence;

public sealed class BookingDbContext : DbContext, IUnitOfWork
{
    private readonly IDomainEventDispatcher _domainEventDispatcher;
    private readonly IClock _clock;
    private readonly Dictionary<IDomainEvent, OutboxMessage> _stagedOutboxMessages =
        new Dictionary<IDomainEvent, OutboxMessage>(ReferenceEqualityComparer.Instance);
    public BookingDbContext(
        DbContextOptions<BookingDbContext> options,
        IDomainEventDispatcher domainEventDispatcher,
        IClock clock) : base(options)
    {
        _domainEventDispatcher = domainEventDispatcher ??
            throw new ArgumentNullException(nameof(domainEventDispatcher));

        _clock = clock ??
            throw new ArgumentNullException(nameof(clock));
    }

    public DbSet<Property> Properties => Set<Property>();
    public DbSet<RentableUnit> RentableUnits => Set<RentableUnit>();
    public DbSet<DomainBooking> Bookings => Set<DomainBooking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    internal DbSet<IdempotencyRequest> IdempotencyRequests => Set<IdempotencyRequest>();
    internal DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();
    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    internal DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(BookingDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(true, cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StageDomainEventsInOutbox();

        int affectedEntries = await base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);

        if (Database.CurrentTransaction is null)
        {
            await DispatchDomainEventsAsync(CancellationToken.None);
        }

        return affectedEntries;
    }

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        await base.SaveChangesAsync(cancellationToken);
    }

    internal async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        IDomainEvent[] domainEvents = DequeueDomainEvents();

        if (domainEvents.Length == 0)
        {
            return;
        }

        await _domainEventDispatcher
            .DispatchAsync(domainEvents, cancellationToken);
    }

    internal void ClearDomainEvents()
    {
        foreach (AggregateRoot aggregateRoot in GetTrackedAggregateRoots())
        {
            aggregateRoot.ClearDomainEvents();
        }

        foreach (OutboxMessage outboxMessage in _stagedOutboxMessages.Values)
        {
            Entry(outboxMessage).State = EntityState.Detached;
        }

        _stagedOutboxMessages.Clear();
    }

    private void StageDomainEventsInOutbox()
    {
        foreach (AggregateRoot aggregateRoot in GetTrackedAggregateRoots())
        {
            foreach (IDomainEvent domainEvent in aggregateRoot.GetDomainEvents())
            {
                if (_stagedOutboxMessages.ContainsKey(domainEvent))
                {
                    continue;
                }

                OutboxMessage message = OutboxMessage.FromDomainEvent(domainEvent, _clock.UtcNow);
                OutboxMessages.Add(message);
                _stagedOutboxMessages.Add(domainEvent, message);
            }
        }
    }

    private IDomainEvent[] DequeueDomainEvents()
    {
        AggregateRoot[] aggregateRoots = GetTrackedAggregateRoots();

        IDomainEvent[] domainEvents = aggregateRoots
                .SelectMany(aggregateRoot => aggregateRoot.GetDomainEvents())
                .ToArray();

        foreach (AggregateRoot aggregateRoot in aggregateRoots)
        {
            aggregateRoot.ClearDomainEvents();
        }

        _stagedOutboxMessages.Clear();

        return domainEvents;
    }

    private AggregateRoot[] GetTrackedAggregateRoots()
    {
        return ChangeTracker
            .Entries()
            .Select(entry => entry.Entity)
            .OfType<AggregateRoot>()
            .ToArray();
    }
}
