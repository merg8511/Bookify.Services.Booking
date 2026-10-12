using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Dapper;
using Microsoft.Extensions.Logging;
using System.Buffers.Binary;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;

namespace Bookify.Services.Booking.Infrastructure.Persistence.Concurrency;

internal sealed class PostgreSqlBackgroundJobClaimManager : IBackgroundJobClaimManager
{
    private const string AcquireLockSql =
        """
        SELECT pg_try_advisory_lock(@LockKey);
        """;

    private const string ReleaseLockSql =
        """
        SELECT pg_advisory_unlock(@LockKey);
        """;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<PostgreSqlBackgroundJobClaimManager> _logger;

    public PostgreSqlBackgroundJobClaimManager(
        IDbConnectionFactory connectionFactory,
        ILogger<PostgreSqlBackgroundJobClaimManager> logger)
    {
        _connectionFactory = connectionFactory ??
            throw new ArgumentNullException(nameof(connectionFactory));

        _logger = logger ??
            throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IBackgroundJobClaim?> TryAcquireAsync(
        string jobKey,
        Guid workItemId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobKey);

        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException("The work item identifier must not be empty.", nameof(workItemId));
        }

        string normalizedJobKey = jobKey.Trim();
        long lockKey = CreateLockKey(normalizedJobKey, workItemId);

        DbConnection connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);

        try
        {
            var command = new CommandDefinition(
                AcquireLockSql,
                new
                {
                    LockKey = lockKey
                },
                cancellationToken: cancellationToken);

            bool acquired = await connection.ExecuteScalarAsync<bool>(command);

            if (!acquired)
            {
                await connection.DisposeAsync();

                return null;
            }

            return new PostgreSqlBackgroundJobClaim(
                connection,
                lockKey,
                normalizedJobKey,
                workItemId,
                _logger);
        }

        catch
        {
            await connection.DisposeAsync();

            throw;
        }
    }

    private static long CreateLockKey(string jobKey, Guid workItemId)
    {
        string resourceKey = $"bookify:background-job:{jobKey}:{workItemId}";
        byte[] resourceBytes = Encoding.UTF8.GetBytes(resourceKey);
        byte[] hash = SHA256.HashData(resourceBytes);

        return BinaryPrimitives.ReadInt64BigEndian(hash.AsSpan(0, sizeof(long)));
    }

    private sealed class PostgreSqlBackgroundJobClaim : IBackgroundJobClaim
    {
        private DbConnection? _connection;

        private readonly long _lockKey;
        private readonly string _jobKey;
        private readonly Guid _workItemId;
        private readonly ILogger<PostgreSqlBackgroundJobClaimManager> _logger;

        public PostgreSqlBackgroundJobClaim(
            DbConnection connection,
            long lockKey,
            string jobKey,
            Guid workItemId,
            ILogger<PostgreSqlBackgroundJobClaimManager> logger)
        {
            _connection = connection ??
                throw new ArgumentNullException(
                    nameof(connection));

            _lockKey = lockKey;
            _jobKey = jobKey;
            _workItemId = workItemId;

            _logger = logger
                ?? throw new ArgumentNullException(nameof(logger));
        }

        public async ValueTask DisposeAsync()
        {
            DbConnection? connection = Interlocked.Exchange(ref _connection, null);

            if (connection is null)
            {
                return;
            }

            try
            {
                if (connection.State == ConnectionState.Open)
                {
                    var command = new CommandDefinition(
                        ReleaseLockSql,
                        new
                        {
                            LockKey = _lockKey
                        },
                        cancellationToken: CancellationToken.None);

                    bool released = await connection.ExecuteScalarAsync<bool>(command);

                    if (!released)
                    {
                        _logger.LogWarning(
                            "PostgreSQL advisory claim was not released because it was no longer held." +
                            "JobKey: {JobKey}. WorkItemId: {WorkItemId}.", _jobKey, _workItemId);
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to explicitly release PostgreSQL background job claim." +
                    "JobKey: {JobKey}. WorkItemId: {WorkItemId}.", _jobKey, _workItemId);
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
