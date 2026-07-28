using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Application.Properties;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;

namespace Bookify.Services.Booking.IntegrationTests.ReadServices;

[Trait("Category",
    "Integration")]
public sealed class PropertyPaginationReadServiceTests
{
    [Fact]
    public async Task GetPagedAsync_ReturnsRequestedPageAndTotals()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using var database =
            new PostgreSqlTestDatabase();

        await database.StartAsync(cancellationToken);

        await using ServiceProvider serviceProvider =
            IntegrationTestServiceProvider.Create(
                database.ConnectionString);

        await using AsyncServiceScope scope =
            serviceProvider.CreateAsyncScope();

        BookingDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<
                    BookingDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);

        IDbConnectionFactory connectionFactory =
            scope.ServiceProvider
                .GetRequiredService<IDbConnectionFactory>();

        await SeedPropertiesAsync(
            connectionFactory,
            cancellationToken);

        IPropertyReadService readService =
            scope.ServiceProvider
            .GetRequiredService<IPropertyReadService>();

        PagedResult<
            PropertyListItemReadModel> page =
            await readService.GetPagedAsync(
                pageNumber: 2,
                pageSize: 2,
                name: null,
                isActive: null,
                cancellationToken);

        Assert.Equal(
            2,
            page.PageNumber);

        Assert.Equal(
            2,
            page.PageSize);

        Assert.Equal(
            5,
            page.TotalRecords);

        Assert.Equal(
            3,
            page.TotalPages);

        Assert.Collection(
            page.Items,
            first =>
            {
                Assert.Equal(
                    "Property 03",
                    first.Name);
            },
            second =>
            {
                Assert.Equal(
                    "Property 04",
                    second.Name);
            });
    }

    private static async Task SeedPropertiesAsync(
       IDbConnectionFactory connectionFactory,
       CancellationToken cancellationToken)
    {
        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        for (int index = 1; index <= 5; index++)
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
                        TRUE
                    );
                    """,
                    new
                    {
                        Id =
                            Guid.NewGuid(),

                        Name =
                            $"Property {index:00}",

                        TimeZoneId =
                            "America/El_Salvador",

                        CheckInTime =
                            new TimeOnly(
                                15,
                                0),

                        CheckOutTime =
                            new TimeOnly(
                                11,
                                0)
                    },
                    cancellationToken:
                        cancellationToken);

            await connection.ExecuteAsync(
                command);
        }
    }
}
