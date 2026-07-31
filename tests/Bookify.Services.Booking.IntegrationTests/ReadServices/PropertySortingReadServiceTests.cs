using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Application.Common.Sorting;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.Properties.GetPaged;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;

namespace Bookify.Services.Booking.IntegrationTests.ReadServices;

[Trait(
    "Category",
    "Integration")]
public sealed class PropertySortingReadServiceTests
{
    [Fact]
    public async Task GetPagedAsync_AppliesWhitelistedSorting()
    {
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        await using var database =
            new PostgreSqlTestDatabase();

        await database.StartAsync(
            cancellationToken);

        await using ServiceProvider serviceProvider =
            IntegrationTestServiceProvider.Create(
                database.ConnectionString);

        await using AsyncServiceScope scope =
            serviceProvider.CreateAsyncScope();

        BookingDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<
                    BookingDbContext>();

        await dbContext.Database.MigrateAsync(
            cancellationToken);

        IDbConnectionFactory connectionFactory =
            scope.ServiceProvider
                .GetRequiredService<
                    IDbConnectionFactory>();

        await SeedPropertiesAsync(
            connectionFactory,
            cancellationToken);

        IPropertyReadService readService =
            scope.ServiceProvider
                .GetRequiredService<
                    IPropertyReadService>();

        PagedResult<
            PropertyListItemReadModel> namePage =
            await readService.GetPagedAsync(
                pageNumber: 1,
                pageSize: 10,
                name: null,
                isActive: null,
                sortField:
                    PropertySortField.Name,
                sortDirection:
                    SortDirection.Descending,
                cancellationToken);

        Assert.Collection(
            namePage.Items,
            first =>
                Assert.Equal(
                    "Property Charlie",
                    first.Name),
            second =>
                Assert.Equal(
                    "Property Bravo",
                    second.Name),
            third =>
                Assert.Equal(
                    "Property Alpha",
                    third.Name));

        PagedResult<
            PropertyListItemReadModel> statusPage =
            await readService.GetPagedAsync(
                pageNumber: 1,
                pageSize: 10,
                name: null,
                isActive: null,
                sortField:
                    PropertySortField.IsActive,
                sortDirection:
                    SortDirection.Descending,
                cancellationToken);

        Assert.Equal(
            3,
            statusPage.Items.Count);

        Assert.True(
            statusPage.Items[0].IsActive);

        Assert.True(
            statusPage.Items[1].IsActive);

        Assert.False(
            statusPage.Items[2].IsActive);
    }

    private static async Task SeedPropertiesAsync(
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken)
    {
        PropertySeed[] properties =
        [
            new(
                "Property Bravo",
                true),

            new(
                "Property Alpha",
                false),

            new(
                "Property Charlie",
                true)
        ];

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        foreach (PropertySeed property
                 in properties)
        {
            var command =
                new CommandDefinition(
                    """
                    INSERT INTO properties
                    (
                        id,
                        name,
                        time_zone_id,
                        check_in_time,
                        check_out_time,
                        is_active
                    )
                    VALUES
                    (
                        @Id,
                        @Name,
                        @TimeZoneId,
                        @CheckInTime,
                        @CheckOutTime,
                        @IsActive
                    );
                    """,
                    new
                    {
                        Id =
                            Guid.NewGuid(),

                        property.Name,

                        TimeZoneId =
                            "America/El_Salvador",

                        CheckInTime =
                            new TimeOnly(
                                15,
                                0),

                        CheckOutTime =
                            new TimeOnly(
                                11,
                                0),

                        property.IsActive
                    },
                    cancellationToken:
                        cancellationToken);

            await connection.ExecuteAsync(
                command);
        }
    }

    private sealed record PropertySeed(
        string Name,
        bool IsActive);
}
