using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Transactions;

internal sealed class EfCoreTransactionManager : ITransactionManager
{
    private readonly BookingDbContext _dbContext;

    public EfCoreTransactionManager(BookingDbContext dbContext)
    {
        _dbContext = dbContext ??
            throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        IDbContextTransaction transaction =
            await _dbContext.Database
                .BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

        return new EfCoreTransaction(transaction);
    }

    private sealed class EfCoreTransaction : ITransaction
    {

        private readonly IDbContextTransaction _transaction;
        private bool _completed;

        public EfCoreTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction ??
                throw new ArgumentNullException(nameof(transaction));
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_completed)
            {
                throw new InvalidOperationException(
                    "The transaction has already completed.");
            }

            await _transaction.CommitAsync(cancellationToken);

            _completed = true;
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (_completed)
            {
                return;
            }

            await _transaction.RollbackAsync(cancellationToken);

            _completed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_completed)
            {
                await RollbackAsync(CancellationToken.None);
            }

            await _transaction.DisposeAsync();
        }


    }
}
