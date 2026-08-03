using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Availability;
using Bookify.Services.Booking.Application.Availability.ReadModels;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;

namespace Bookify.Services.Booking.IntegrationTests.ReadServices;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class
    DapperAvailabilityStatusFilteringTests
{
    private readonly BookingApiFactory _factory;

    public DapperAvailabilityStatusFilteringTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetInventoryConflictsAsync_ReturnsOnlyBookingsThatBlockInventory()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        TestData data = await SeedAsync(cancellationToken);

        using IServiceScope scope = _factory.Services.CreateScope();

        IAvailabilityReadService readService =
            scope.ServiceProvider
                .GetRequiredService<
                    IAvailabilityReadService>();

        // Act
        IReadOnlyList<
            OverlappingBookingReadModel> result =
            await readService
                .GetInventoryConflictsAsync(
                    data.PropertyId,
                    data.RentableUnitId,
                    Date(10),
                    Date(15),
                    cancellationToken);

        // Assert
        HashSet<Guid> actualBookingIds =
            result
                .Select(
                    booking =>
                        booking.BookingId)
                .ToHashSet();

        Assert.Equal(data.ExpectedConflictIds, actualBookingIds);

        Assert.DoesNotContain(
            data.CancelledBookingId,
            actualBookingIds);

        HashSet<string> returnedStatuses =
            result
                .Select(booking => booking.Status)
                .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            new HashSet<string> { "PendingApproval", "PendingPayment", "Paid", "Completed" },
            returnedStatuses);
    }

    private async Task<TestData> SeedAsync(CancellationToken cancellationToken)
    {
        Guid propertyId = Guid.NewGuid();
        Guid rentableUnitId = Guid.NewGuid();
        Guid pendingApprovalBookingId = Guid.NewGuid();
        Guid pendingPaymentBookingId = Guid.NewGuid();
        Guid paidBookingId = Guid.NewGuid();
        Guid completedBookingId = Guid.NewGuid();
        Guid cancelledBookingId = Guid.NewGuid();

        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

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
                    @PropertyId,
                    'Status Filtering Property',
                    'America/El_Salvador',
                    '15:00',
                    '11:00',
                    TRUE
                );

                INSERT INTO rentable_units
                (
                    id,
                    property_id,
                    name,
                    type,
                    maximum_capacity,
                    max_base_guests,
                    is_active
                )
                VALUES
                (
                    @RentableUnitId,
                    @PropertyId,
                    'Room A',
                    'Room',
                    4,
                    2,
                    TRUE
                );

                INSERT INTO bookings
                (
                    id,
                    property_id,
                    rentable_unit_id,
                    check_in_date,
                    check_out_date,
                    guest_count,
                    status,
                    cancellation_reason
                )
                VALUES
                (
                    @PendingApprovalBookingId,
                    @PropertyId,
                    @RentableUnitId,
                    @CheckInDate,
                    @CheckOutDate,
                    2,
                    'PendingApproval',
                    NULL
                ),
                (
                    @PendingPaymentBookingId,
                    @PropertyId,
                    @RentableUnitId,
                    @CheckInDate,
                    @CheckOutDate,
                    2,
                    'PendingPayment',
                    NULL
                ),
                (
                    @PaidBookingId,
                    @PropertyId,
                    @RentableUnitId,
                    @CheckInDate,
                    @CheckOutDate,
                    2,
                    'Paid',
                    NULL
                ),
                (
                    @CompletedBookingId,
                    @PropertyId,
                    @RentableUnitId,
                    @CheckInDate,
                    @CheckOutDate,
                    2,
                    'Completed',
                    NULL
                ),
                (
                    @CancelledBookingId,
                    @PropertyId,
                    @RentableUnitId,
                    @CheckInDate,
                    @CheckOutDate,
                    2,
                    'Cancelled',
                    'PaymentExpired'
                );
                """,
                new
                {
                    PropertyId = propertyId,
                    RentableUnitId = rentableUnitId,
                    PendingApprovalBookingId = pendingApprovalBookingId,
                    PendingPaymentBookingId = pendingPaymentBookingId,
                    PaidBookingId = paidBookingId,
                    CompletedBookingId = completedBookingId,
                    CancelledBookingId = cancelledBookingId,
                    CheckInDate = Date(11),
                    CheckOutDate = Date(14)
                },
                cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);

        return new TestData(
            propertyId,
            rentableUnitId,
            new HashSet<Guid>
            {
                pendingApprovalBookingId,
                pendingPaymentBookingId,
                paidBookingId,
                completedBookingId
            },
            cancelledBookingId);
    }

    private static DateOnly Date(int day)
    {
        return new DateOnly(
            2026,
            8,
            day);
    }

    private sealed record TestData(
        Guid PropertyId,
        Guid RentableUnitId,
        IReadOnlySet<Guid> ExpectedConflictIds,
        Guid CancelledBookingId);
}
