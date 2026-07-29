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

[Trait("Category", "Integration")]
public sealed class PropertyFilteringReadServiceTests
{
    [Fact]
    public async Task GetPagedAsync_AppliesOptionalFiltersToItemsAndCount()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using var database = new PostgreSqlTestDatabase();

        await database.StartAsync(cancellationToken);

        await using ServiceProvider serviceProvider =
            IntegrationTestServiceProvider.Create(
                database.ConnectionString);

        await using AsyncServiceScope scope =
            serviceProvider.CreateAsyncScope();

        BookingDbContext dbContext =
            scope.ServiceProvider
                .GetRequiredService<BookingDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);

        IDbConnectionFactory connectionFactory =
            scope.ServiceProvider
                .GetRequiredService<IDbConnectionFactory>();

        await SeedPropertiesAsync(connectionFactory, cancellationToken);

        IPropertyReadService readService =
            scope.ServiceProvider
                .GetRequiredService<IPropertyReadService>();

        PagedResult<
            PropertyListItemReadModel> namePage =
                await readService.GetPagedAsync(
                    pageNumber: 1,
                    pageSize: 10,
                    name: "RANCHO",
                    isActive: null,
                    sortField: PropertySortField.Name,
                    sortDirection: SortDirection.Ascending,
                    cancellationToken);

        Assert.Equal(
            3,
            namePage.TotalRecords);

        Assert.Equal(
            3,
            namePage.Items.Count);

        Assert.All(
            namePage.Items,
            property =>
                Assert.True(
                    property.Name.Contains("rancho",
                        StringComparison
                            .OrdinalIgnoreCase)));

        PagedResult<
            PropertyListItemReadModel> inactivePage =
                await readService.GetPagedAsync(
                    pageNumber: 1,
                    pageSize: 10,
                    name: null,
                    isActive: false,
                    sortField: PropertySortField.Name,
                    sortDirection: SortDirection.Ascending,
                    cancellationToken);

        PropertyListItemReadModel
            inactiveProperty =
                Assert.Single(inactivePage.Items);

        Assert.Equal(
            1,
            inactivePage.TotalRecords);

        Assert.Equal(
            "Rancho Verde",
            inactiveProperty.Name);

        Assert.False(inactiveProperty.IsActive);

        PagedResult<PropertyListItemReadModel> combinedPage =
            await readService.GetPagedAsync(
                pageNumber: 1,
                pageSize: 1,
                name: "rancho",
                isActive: true,
                sortField: PropertySortField.Name,
                sortDirection: SortDirection.Ascending,
                cancellationToken);

        Assert.Single(combinedPage.Items);

        Assert.Equal(
            2,
            combinedPage.TotalRecords);

        Assert.Equal(
            2,
            combinedPage.TotalPages);

        PagedResult<
            PropertyListItemReadModel> literalPage =
                await readService.GetPagedAsync(
                    pageNumber: 1,
                    pageSize: 10,
                    name: "%",
                    isActive: null,
                    sortField: PropertySortField.Name,
                    sortDirection: SortDirection.Ascending,
                    cancellationToken);

        PropertyListItemReadModel
            literalProperty = Assert.Single(literalPage.Items);

        Assert.Equal(
            1,
            literalPage.TotalRecords);

        Assert.Equal(
            "100% Natural",
            literalProperty.Name);
    }

    private static async Task SeedPropertiesAsync(
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken)
    {
        PropertySeed[] properties =
            [
                new(
                    "Rancho Azul",
                    true),
                new(
                    "Rancho Verde",
                    false),
                new(
                    "Casa Rancho",
                    true),
                new(
                    "Hotel Centro",
                    true),
                new(
                    "100% Natural",
                    true),
            ];

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(cancellationToken);

        foreach (PropertySeed property in properties)
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
                    )
                    """,
                    new
                    {
                        Id = Guid.NewGuid(),
                        property.Name,
                        TimeZoneId = "America/El_Salvador",
                        CheckInTime = new TimeOnly(15, 0),
                        CheckOutTime = new TimeOnly(11, 0),
                        property.IsActive
                    },
                    cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);
        }
    }

    private sealed record PropertySeed(
        string Name, bool IsActive);
}
