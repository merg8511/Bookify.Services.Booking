using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Properties.Create;
using Bookify.Services.Booking.Application.Properties.GetById;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.IntegrationTests.Properties;

public sealed class CreatePropertyPersistenceTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExecuteAsync_WithValidCommand_ShouldPersistPropertyAndReadItFromNewScope()
    {
        // ARRANGE
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        CancellationToken cancellationToken = cancellationTokenSource.Token;

        await using var database = new PostgreSqlTestDatabase();

        await database.StartAsync(cancellationToken);

        await using ServiceProvider serviceProvider =
            IntegrationTestServiceProvider.Create(
                database.ConnectionString);

        await IntegrationTestServiceProvider
            .ApplyMigrationsAsync(
                serviceProvider,
                cancellationToken);

        BookingDbContext? writeDbContext;

        Guid propertyId;

        // ACT: escribir desde primer scope.
        await using (AsyncServiceScope writeScope =
            serviceProvider.CreateAsyncScope())
        {
            writeDbContext = writeScope.ServiceProvider
                .GetRequiredService<BookingDbContext>();

            var command = new CreatePropertyCommand(
                "  Rancho Costa Azul  ",
                "  America/El_Salvador  ",
                new TimeOnly(15, 0),
                new TimeOnly(11, 0));

            var commandExecutor =
                writeScope.ServiceProvider
                    .GetRequiredService<
                        ICommandExecutor<
                            CreatePropertyCommand,
                            Guid>>();

            Result<Guid> creationResult = await commandExecutor
                .ExecuteAsync(command, cancellationToken);

            Assert.True(
                creationResult.IsSuccess);

            Assert.NotEqual(
                Guid.Empty,
                creationResult.Value);

            propertyId = creationResult.Value;

            Property? trackedProperty =
                writeDbContext.ChangeTracker
                    .Entries<Property>()
                    .Select(
                        entry => entry.Entity)
                    .SingleOrDefault(
                        property =>
                            property.Id == propertyId);

            Assert.NotNull(trackedProperty);

            Assert.Equal(
                EntityState.Unchanged,
                writeDbContext.Entry(
                    trackedProperty).State);
        }

        // ACT: leer desde un scope y DbContext distintos.
        await using (
            AsyncServiceScope readScope =
            serviceProvider.CreateAsyncScope())
        {
            BookingDbContext readDbContext =
                readScope.ServiceProvider
                    .GetRequiredService<BookingDbContext>();

            Assert.NotSame(
                writeDbContext,
                readDbContext);

            var query = new GetPropertyByIdQuery(propertyId);

            var queryExecutor =
                readScope.ServiceProvider
                    .GetRequiredService<
                        IQueryExecutor<
                            GetPropertyByIdQuery,
                            PropertyResponse>>();

            Result<PropertyResponse> queryResult =
                await queryExecutor.ExecuteAsync(
                    query,
                    cancellationToken);

            // ASSERT
            Assert.True(queryResult.IsSuccess);

            Assert.Equal(
                "Rancho Costa Azul",
                queryResult.Value.Name);

            Assert.Equal(
                "America/El_Salvador",
                queryResult.Value.TimeZoneId);

            Assert.Equal(
                new TimeOnly(15, 0),
                queryResult.Value.CheckInTime);

            Assert.Equal(
                new TimeOnly(11, 0),
                queryResult.Value.CheckOutTime);

            Assert.True(
                queryResult.Value.IsActive);

            Assert.Empty(
                readDbContext.ChangeTracker
                    .Entries<Property>());
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExecuteAsync_WithInvalidName_ShouldNotPersistProperty()
    {
        // ARRANGE
        using var cancellationTokenSource =
            new CancellationTokenSource(
                TimeSpan.FromMinutes(2));

        CancellationToken cancellationToken =
            cancellationTokenSource.Token;

        await using var database =
            new PostgreSqlTestDatabase();

        await database.StartAsync(
            cancellationToken);

        await using ServiceProvider serviceProvider =
            IntegrationTestServiceProvider.Create(
                database.ConnectionString);

        await IntegrationTestServiceProvider
            .ApplyMigrationsAsync(
                serviceProvider,
                cancellationToken);

        await using AsyncServiceScope scope =
            serviceProvider.CreateAsyncScope();

        var executor =
            scope.ServiceProvider
                .GetRequiredService<
                    ICommandExecutor<
                        CreatePropertyCommand,
                        Guid>>();

        var command =
            new CreatePropertyCommand(
                "   ",
                "America/El_Salvador",
                new TimeOnly(15, 0),
                new TimeOnly(11, 0));

        // ACT
        Result<Guid> result =
            await executor.ExecuteAsync(
                command,
                cancellationToken);

        // ASSERT
        Assert.True(
            result.IsFailure);

        BookingDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<
                    BookingDbContext>();

        int propertyCount =
            await dbContext.Properties.CountAsync(
                cancellationToken);

        Assert.Equal(
            0,
            propertyCount);
    }
}
