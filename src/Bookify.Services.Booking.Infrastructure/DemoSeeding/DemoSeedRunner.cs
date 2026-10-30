using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bookify.Services.Booking.Infrastructure.DemoSeeding;

public sealed class DemoSeedRunner
{
    private readonly BookingDbContext _dbContext;
    private readonly ITransactionManager _transactionManager;
    private readonly IClock _clock;
    private readonly IDemoSeedBatch[] _batches;
    private readonly ILogger<DemoSeedRunner> _logger;

    public DemoSeedRunner(
        BookingDbContext dbContext,
        ITransactionManager transactionManager,
        IClock clock,
        IEnumerable<IDemoSeedBatch> batches,
        ILogger<DemoSeedRunner> logger)
    {
        _dbContext = dbContext ??
            throw new ArgumentNullException(nameof(dbContext));

        _transactionManager = transactionManager ??
            throw new ArgumentNullException(nameof(transactionManager));

        _clock = clock ??
            throw new ArgumentNullException(nameof(clock));

        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));

        ArgumentNullException.ThrowIfNull(batches);

        _batches = batches.ToArray();

        ValidateBatchKeys(_batches);
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string[] pendingMigrations = (await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        if (pendingMigrations.Length > 0)
        {
            throw new InvalidOperationException("Demo seed requires all database migrations " +
                "to be applied before execution.");
        }

        if (_batches.Length == 0)
        {
            _logger.LogInformation("Demo seed is enabled, but no seed batches are currenly registered.");

            return 0;
        }

        int appliedBatches = 0;

        foreach (IDemoSeedBatch batch in _batches.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using ITransaction transaction = await _transactionManager.BeginAsync(cancellationToken);

            DateTimeOffset appliedAtUtc = _clock.UtcNow.ToUniversalTime();

            int inserted = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO demo_seed_batches (batch_key, applied_at_utc)
                VALUES ({batch.Key}, {appliedAtUtc})
                ON CONFLICT (batch_key) DO NOTHING;
                """,
                cancellationToken);

            if (inserted == 0)
            {
                await transaction.RollbackAsync(cancellationToken);

                _logger.LogInformation("Demo seed batch {BatchKey} was already applied.", batch.Key);

                continue;
            }

            if (inserted != 1)
            {
                throw new InvalidOperationException("Unexpected demo seed batch insertion result.");
            }

            await batch.ExecuteAsync(_dbContext, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            appliedBatches++;

            _logger.LogInformation("Demo seed batch {BatchKey} applied successfully.", batch.Key);
        }

        _logger.LogInformation("Demo seed finished. Applied batches: {AppliedCount}.", appliedBatches);

        return appliedBatches;
    }

    private static void ValidateBatchKeys(IReadOnlyList<IDemoSeedBatch> batches)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (IDemoSeedBatch batch in batches)
        {
            if (batch is null)
            {
                throw new InvalidOperationException("Demo seed batches cannot contain a null entries.");
            }

            string? key = batch.Key;

            bool valid = !string.IsNullOrWhiteSpace(key) &&
                         key.Length <= 128 &&
                         key.All(character =>
                                (character >= 'a' && character <= 'z') ||
                                (character >= '0' && character <= '9') ||
                                character is '.' or '-' or '_');

            if (!valid)
            {
                throw new InvalidOperationException(
                    "Demo seed batch keys must use lowercase letters, digits, dots, hyphens or underscores " +
                    "and must not exceed 128 characters.");
            }

            if (!keys.Add(key!))
            {
                throw new InvalidOperationException($"Duplicate demo seed batch key '{key}'.");
            }
        }
    }
}
