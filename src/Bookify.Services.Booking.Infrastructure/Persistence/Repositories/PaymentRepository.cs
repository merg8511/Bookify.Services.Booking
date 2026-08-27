using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Repositories;

internal sealed class PaymentRepository : IPaymentRepository
{

    private readonly BookingDbContext _dbContext;

    public PaymentRepository(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Payment?> GetByBookingIdAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Payments
            .Include(payment => payment.Attempts)
            .SingleOrDefaultAsync(payment => payment.BookingId == bookingId, cancellationToken);
    }

    public void Add(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        _dbContext.Payments.Add(payment);
    }


}
