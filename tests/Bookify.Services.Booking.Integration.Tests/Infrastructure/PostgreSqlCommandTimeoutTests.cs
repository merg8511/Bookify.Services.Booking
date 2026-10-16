
using System.Data.Common;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.Infrastructure;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class PostgreSqlCommandTimeoutTests
{
    private readonly BookingApiFactory _factory;

    public PostgreSqlCommandTimeoutTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SlowCommand_ShouldRespectConfiguredTimeout()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        IConfiguration configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Payments:Provider"] = "Fake",
                        ["PostgreSql:ConnectionTimeoutSeconds"] = "10",
                        ["PostgreSql:CommandTimeoutSeconds"] = "1"
                    })
                .Build();

        await using ServiceProvider services =
            IntegrationTestServiceProvider.Create(
                _factory.DatabaseConnectionString,
                configuration);

        IDbConnectionFactory connectionFactory =
            services.GetRequiredService<IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory.OpenConnectionAsync(
                cancellationToken);

        await using DbCommand command =
            connection.CreateCommand();

        command.CommandText = "SELECT pg_sleep(3);";

        Assert.Equal(1, command.CommandTimeout);

        Exception? exception =
            await Record.ExceptionAsync(
                async () =>
                {
                    await command.ExecuteNonQueryAsync(
                        cancellationToken);
                });

        Assert.NotNull(exception);

        Assert.False(
            cancellationToken.IsCancellationRequested);

        Assert.True(
            exception is DbException or
                         OperationCanceledException or
                         TimeoutException,
            $"Unexpected timeout exception type: " +
            $"{exception.GetType().Name}");
    }
}
