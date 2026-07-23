using Testcontainers.PostgreSql;

namespace Bookify.Services.Booking.IntegrationTests.Infrastructure;

internal sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container;

    public PostgreSqlTestDatabase()
    {
        _container =
            new PostgreSqlBuilder(
                "postgres:18.4-alpine")
            .WithDatabase(
                "bookify_booking_tests")
            .WithUsername(
                "bookify")
            .WithPassword(
                "bookify-integration-tests")
            .Build();
    }

    public string ConnectionString =>
        _container.GetConnectionString();

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return _container.StartAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return _container.DisposeAsync();
    }
}
