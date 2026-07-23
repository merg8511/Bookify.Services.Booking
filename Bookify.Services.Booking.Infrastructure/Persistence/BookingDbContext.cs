using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;
using Bookify.Services.Booking.Domain.Properties;
using Microsoft.EntityFrameworkCore;
using Bookify.Services.Booking.Application.Abstractions.Persistence;

namespace Bookify.Services.Booking.Infrastructure.Persistence;

public sealed class BookingDbContext : DbContext, IUnitOfWork
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {

    }

    public DbSet<Property> Properties =>
        Set<Property>();

    public DbSet<RentableUnit> RentableUnits =>
        Set<RentableUnit>();

    public DbSet<DomainBooking> Bookings =>
        Set<DomainBooking>();

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
    }
}
