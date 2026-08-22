using Bookify.Services.Booking.Application.Abstractions.DomainEvents;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared.DomainEvents;
using Bookify.Services.Booking.Infrastructure.Persistence.Idempotency;
using Microsoft.EntityFrameworkCore;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Infrastructure.Persistence;

public sealed class BookingDbContext : DbContext, IUnitOfWork
{
    private readonly IDomainEventDispatcher _domainEventDispatcher;
    public BookingDbContext(
        DbContextOptions<BookingDbContext> options,
        IDomainEventDispatcher domainEventDispatcher) : base(options)
    {
        _domainEventDispatcher = domainEventDispatcher ??
            throw new ArgumentNullException(nameof(domainEventDispatcher));
    }

    public DbSet<Property> Properties =>
        Set<Property>();

    public DbSet<RentableUnit> RentableUnits =>
        Set<RentableUnit>();

    public DbSet<DomainBooking> Bookings =>
        Set<DomainBooking>();

    internal DbSet<IdempotencyRequest> IdempotencyRequests =>
        Set<IdempotencyRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(BookingDbContext).Assembly);
    }

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        await base.SaveChangesAsync(cancellationToken);

        if (Database.CurrentTransaction is null)
        {
            await DispatchDomainEventsAsync(CancellationToken.None);
        }
    }

    internal async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<IDomainEvent> domainEvents = DequeueDomainEvents();

        if (domainEvents.Count == 0)
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
    }

    private IReadOnlyCollection<IDomainEvent> DequeueDomainEvents()
    {
        AggregateRoot[] aggregateRoots = GetTrackedAggregateRoots();

        IDomainEvent[] domainEvents =
            aggregateRoots
                .SelectMany(
                    aggregateRoot =>
                        aggregateRoot.GetDomainEvents())
                .ToArray();

        foreach (AggregateRoot aggregateRoot in aggregateRoots)
        {
            aggregateRoot.ClearDomainEvents();
        }

        return domainEvents;
    }

    private AggregateRoot[] GetTrackedAggregateRoots()
    {
        return ChangeTracker
            .Entries()
            .Select(
                entry => entry.Entity)
            .OfType<AggregateRoot>()
            .ToArray();
    }
}
