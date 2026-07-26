using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Data.Common;

namespace Bookify.Services.Booking.IntegrationTests.Infrastructure;

[Trait(
    "Category",
    "Integration")]
public sealed class ReadConnectionFactoryTests
{
    [Fact]
    public async Task OpenConnectionAsync_ReturnsOpenConnectionToExpectedDatabase()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using var database = new PostgreSqlTestDatabase();

        await database.StartAsync(cancellationToken);

        await using ServiceProvider serviceProvider =
            IntegrationTestServiceProvider.Create(database.ConnectionString);

        IDbConnectionFactory connectionFactory =
            serviceProvider
            .GetRequiredService<IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(cancellationToken);

        Assert.Equal(
            ConnectionState.Open,
            connection.State);

        await using DbCommand command = connection.CreateCommand();

        command.CommandText = "SELECT current_database();";

        object? scalar =
            await command.ExecuteScalarAsync(cancellationToken);

        string databaseName =
            Assert.IsType<string>(scalar);

        Assert.Equal(
            "bookify_booking_tests",
            databaseName);
    }
}
