using System.Data.Common;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.BackgroundJobs;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.BackgroundJobs;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class PendingBookingWorkSelectorCoverageTests
{
    private static readonly DateTimeOffset NowUtc =
        new(
            2025,
            6,
            15,
            15,
            0,
            0,
            TimeSpan.Zero);

    private static readonly DateTimeOffset CreatedAtUtc =
        new(
            2025,
            6,
            13,
            12,
            0,
            0,
            TimeSpan.Zero);

    private static readonly DateTimeOffset ApprovedAtUtc =
        new(
            2025,
            6,
            14,
            12,
            0,
            0,
            TimeSpan.Zero);

    private readonly BookingApiFactory _factory;

    public PendingBookingWorkSelectorCoverageTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetExpiredApprovalBookingIdsAsync_ShouldUseInclusiveDeadlineAndPendingApprovalStatus()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking expired =
            await SeedBookingAsync(
                BookingStatus.PendingApproval,
                approvalDueAtUtc:
                    NowUtc.AddMinutes(-1),
                cancellationToken:
                    cancellationToken);

        DomainBooking boundary =
            await SeedBookingAsync(
                BookingStatus.PendingApproval,
                approvalDueAtUtc:
                    NowUtc,
                cancellationToken:
                    cancellationToken);

        DomainBooking future =
            await SeedBookingAsync(
                BookingStatus.PendingApproval,
                approvalDueAtUtc:
                    NowUtc.AddMinutes(1),
                cancellationToken:
                    cancellationToken);

        DomainBooking wrongStatus =
            await SeedBookingAsync(
                BookingStatus.PendingPayment,
                approvalDueAtUtc:
                    NowUtc.AddMinutes(-1),
                paymentDueAtUtc:
                    NowUtc.AddHours(1),
                cancellationToken:
                    cancellationToken);

        using IServiceScope scope =
            _factory.Services.CreateScope();

        IPendingBookingWorkSelector selector =
            scope.ServiceProvider
                .GetRequiredService<
                    IPendingBookingWorkSelector>();

        IReadOnlyList<Guid> ids =
            await selector
                .GetExpiredApprovalBookingIdsAsync(
                    NowUtc,
                    batchSize: 10_000,
                    cancellationToken);

        Assert.Contains(expired.Id, ids);
        Assert.Contains(boundary.Id, ids);
        Assert.DoesNotContain(future.Id, ids);
        Assert.DoesNotContain(wrongStatus.Id, ids);
    }

    [Fact]
    public async Task GetExpiredPaymentBookingIdsAsync_ShouldUseInclusiveDeadlineAndPendingPaymentStatus()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DomainBooking expired =
            await SeedBookingAsync(
                BookingStatus.PendingPayment,
                paymentDueAtUtc:
                    NowUtc.AddMinutes(-1),
                cancellationToken:
                    cancellationToken);

        DomainBooking boundary =
            await SeedBookingAsync(
                BookingStatus.PendingPayment,
                paymentDueAtUtc:
                    NowUtc,
                cancellationToken:
                    cancellationToken);

        DomainBooking future =
            await SeedBookingAsync(
                BookingStatus.PendingPayment,
                paymentDueAtUtc:
                    NowUtc.AddMinutes(1),
                cancellationToken:
                    cancellationToken);

        DomainBooking wrongStatus =
            await SeedBookingAsync(
                BookingStatus.Paid,
                paymentDueAtUtc:
                    NowUtc.AddMinutes(-1),
                cancellationToken:
                    cancellationToken);

        using IServiceScope scope =
            _factory.Services.CreateScope();

        IPendingBookingWorkSelector selector =
            scope.ServiceProvider
                .GetRequiredService<
                    IPendingBookingWorkSelector>();

        IReadOnlyList<Guid> ids =
            await selector
                .GetExpiredPaymentBookingIdsAsync(
                    NowUtc,
                    batchSize: 10_000,
                    cancellationToken);

        Assert.Contains(expired.Id, ids);
        Assert.Contains(boundary.Id, ids);
        Assert.DoesNotContain(future.Id, ids);
        Assert.DoesNotContain(wrongStatus.Id, ids);
    }

    [Fact]
    public async Task GetDueCompletionBookingIdsAsync_ShouldUsePropertyTimeZoneAndPaidStatus()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DateOnly checkOutDate =
            new(2025, 6, 15);

        DomainBooking boundary =
            await SeedBookingAsync(
                BookingStatus.Paid,
                checkInDate:
                    checkOutDate.AddDays(-2),
                checkOutDate:
                    checkOutDate,
                timeZoneId:
                    "America/New_York",
                checkOutTime:
                    new TimeOnly(11, 0),
                cancellationToken:
                    cancellationToken);

        // June in New York is UTC-4.
        // 11:01 local = 15:01 UTC, therefore still future.
        DomainBooking future =
            await SeedBookingAsync(
                BookingStatus.Paid,
                checkInDate:
                    checkOutDate.AddDays(-2),
                checkOutDate:
                    checkOutDate,
                timeZoneId:
                    "America/New_York",
                checkOutTime:
                    new TimeOnly(11, 1),
                cancellationToken:
                    cancellationToken);

        DomainBooking wrongStatus =
            await SeedBookingAsync(
                BookingStatus.PendingPayment,
                paymentDueAtUtc:
                    NowUtc.AddHours(2),
                checkInDate:
                    checkOutDate.AddDays(-2),
                checkOutDate:
                    checkOutDate,
                timeZoneId:
                    "America/New_York",
                checkOutTime:
                    new TimeOnly(11, 0),
                cancellationToken:
                    cancellationToken);

        using IServiceScope scope =
            _factory.Services.CreateScope();

        IPendingBookingWorkSelector selector =
            scope.ServiceProvider
                .GetRequiredService<
                    IPendingBookingWorkSelector>();

        IReadOnlyList<Guid> ids =
            await selector
                .GetDueCompletionBookingIdsAsync(
                    NowUtc,
                    batchSize: 10_000,
                    cancellationToken);

        Assert.Contains(boundary.Id, ids);
        Assert.DoesNotContain(future.Id, ids);
        Assert.DoesNotContain(wrongStatus.Id, ids);
    }

    [Fact]
    public async Task GetPaymentDeadlineReminderCandidateIdsAsync_ShouldUseHalfOpenWindowAndPendingPaymentStatus()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DateTimeOffset windowStart =
            NowUtc.AddMinutes(10);

        DateTimeOffset windowEnd =
            NowUtc.AddMinutes(20);

        DomainBooking boundaryStart =
            await SeedBookingAsync(
                BookingStatus.PendingPayment,
                paymentDueAtUtc:
                    windowStart,
                cancellationToken:
                    cancellationToken);

        DomainBooking inside =
            await SeedBookingAsync(
                BookingStatus.PendingPayment,
                paymentDueAtUtc:
                    windowEnd.AddSeconds(-1),
                cancellationToken:
                    cancellationToken);

        DomainBooking boundaryEnd =
            await SeedBookingAsync(
                BookingStatus.PendingPayment,
                paymentDueAtUtc:
                    windowEnd,
                cancellationToken:
                    cancellationToken);

        DomainBooking before =
            await SeedBookingAsync(
                BookingStatus.PendingPayment,
                paymentDueAtUtc:
                    windowStart.AddSeconds(-1),
                cancellationToken:
                    cancellationToken);

        DomainBooking wrongStatus =
            await SeedBookingAsync(
                BookingStatus.Paid,
                paymentDueAtUtc:
                    windowStart,
                cancellationToken:
                    cancellationToken);

        using IServiceScope scope =
            _factory.Services.CreateScope();

        IPendingBookingWorkSelector selector =
            scope.ServiceProvider
                .GetRequiredService<
                    IPendingBookingWorkSelector>();

        IReadOnlyList<Guid> ids =
            await selector
                .GetPaymentDeadlineReminderCandidateIdsAsync(
                    windowStart,
                    windowEnd,
                    batchSize: 10_000,
                    cancellationToken);

        Assert.Contains(boundaryStart.Id, ids);
        Assert.Contains(inside.Id, ids);
        Assert.DoesNotContain(boundaryEnd.Id, ids);
        Assert.DoesNotContain(before.Id, ids);
        Assert.DoesNotContain(wrongStatus.Id, ids);
    }

    [Fact]
    public async Task GetUpcomingCheckInReminderCandidateIdsAsync_ShouldUseTimeZoneHalfOpenWindowAndPaidStatus()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DateTimeOffset windowStart =
            new(
                2025,
                6,
                16,
                15,
                0,
                0,
                TimeSpan.Zero);

        DateTimeOffset windowEnd =
            windowStart.AddMinutes(30);

        DateOnly checkInDate =
            new(2025, 6, 16);

        DomainBooking boundaryStart =
            await SeedBookingAsync(
                BookingStatus.Paid,
                checkInDate:
                    checkInDate,
                checkOutDate:
                    checkInDate.AddDays(2),
                timeZoneId:
                    "America/New_York",
                checkInTime:
                    new TimeOnly(11, 0),
                cancellationToken:
                    cancellationToken);

        DomainBooking inside =
            await SeedBookingAsync(
                BookingStatus.Paid,
                checkInDate:
                    checkInDate,
                checkOutDate:
                    checkInDate.AddDays(2),
                timeZoneId:
                    "America/New_York",
                checkInTime:
                    new TimeOnly(11, 29),
                cancellationToken:
                    cancellationToken);

        DomainBooking boundaryEnd =
            await SeedBookingAsync(
                BookingStatus.Paid,
                checkInDate:
                    checkInDate,
                checkOutDate:
                    checkInDate.AddDays(2),
                timeZoneId:
                    "America/New_York",
                checkInTime:
                    new TimeOnly(11, 30),
                cancellationToken:
                    cancellationToken);

        DomainBooking wrongStatus =
            await SeedBookingAsync(
                BookingStatus.PendingPayment,
                paymentDueAtUtc:
                    NowUtc.AddHours(4),
                checkInDate:
                    checkInDate,
                checkOutDate:
                    checkInDate.AddDays(2),
                timeZoneId:
                    "America/New_York",
                checkInTime:
                    new TimeOnly(11, 0),
                cancellationToken:
                    cancellationToken);

        using IServiceScope scope =
            _factory.Services.CreateScope();

        IPendingBookingWorkSelector selector =
            scope.ServiceProvider
                .GetRequiredService<
                    IPendingBookingWorkSelector>();

        IReadOnlyList<Guid> ids =
            await selector
                .GetUpcomingCheckInReminderCandidateIdsAsync(
                    windowStart,
                    windowEnd,
                    batchSize: 10_000,
                    cancellationToken);

        Assert.Contains(boundaryStart.Id, ids);
        Assert.Contains(inside.Id, ids);
        Assert.DoesNotContain(boundaryEnd.Id, ids);
        Assert.DoesNotContain(wrongStatus.Id, ids);
    }

    [Fact]
    public async Task PendingWorkIndexes_ShouldExist()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        IDbConnectionFactory connectionFactory =
            _factory.Services
                .GetRequiredService<
                    IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory
                .OpenConnectionAsync(
                    cancellationToken);

        IReadOnlyList<IndexRow> indexes =
            (
                await connection.QueryAsync<IndexRow>(
                    new CommandDefinition(
                        """
                        SELECT
                            indexname AS "IndexName",
                            indexdef AS "IndexDefinition"
                        FROM pg_indexes
                        WHERE schemaname = 'public'
                          AND tablename IN
                          (
                              'bookings',
                              'outbox_messages'
                          );
                        """,
                        cancellationToken:
                            cancellationToken))
            )
            .ToArray();

        Assert.Contains(
            indexes,
            index =>
                string.Equals(
                    index.IndexName,
                    "ix_bookings_pending_approval_due_at_utc",
                    StringComparison.Ordinal));

        Assert.Contains(
            indexes,
            index =>
                string.Equals(
                    index.IndexName,
                    "ix_bookings_pending_payment_due_at_utc",
                    StringComparison.Ordinal));

        Assert.Contains(
            indexes,
            index =>
                string.Equals(
                    index.IndexName,
                    "ix_bookings_paid_check_out_date",
                    StringComparison.Ordinal));

        Assert.Contains(
            indexes,
            index =>
                string.Equals(
                    index.IndexName,
                    "ix_bookings_paid_check_in_date",
                    StringComparison.Ordinal));

        Assert.Contains(
            indexes,
            index =>
                index.IndexDefinition.Contains(
                    "outbox_messages",
                    StringComparison.OrdinalIgnoreCase) &&
                index.IndexDefinition.Contains(
                    "created_at_utc",
                    StringComparison.OrdinalIgnoreCase) &&
                index.IndexDefinition.Contains(
                    "processed_at_utc",
                    StringComparison.OrdinalIgnoreCase) &&
                index.IndexDefinition.Contains(
                    "IS NULL",
                    StringComparison.OrdinalIgnoreCase));
    }

    private async Task<DomainBooking> SeedBookingAsync(
        BookingStatus status,
        DateTimeOffset? approvalDueAtUtc = null,
        DateTimeOffset? paymentDueAtUtc = null,
        DateOnly? checkInDate = null,
        DateOnly? checkOutDate = null,
        string timeZoneId = "America/El_Salvador",
        TimeOnly? checkInTime = null,
        TimeOnly? checkOutTime = null,
        CancellationToken cancellationToken = default)
    {
        Property property =
            Property.Create(
                $"Pending Work {Guid.NewGuid():N}",
                timeZoneId,
                checkInTime ?? new TimeOnly(15, 0),
                checkOutTime ?? new TimeOnly(11, 0),
                TestIdentitySubjects.Owner)
            .Value;

        RentableUnit rentableUnit =
            RentableUnit.Create(
                property.Id,
                $"Room {Guid.NewGuid():N}",
                RentableUnitType.Room,
                maximumCapacity: 4,
                maxBaseGuests: 2)
            .Value;

        rentableUnit.ConfigurePricing(
            RentableUnitPricing.Create(
                Money.Create(100m, "USD").Value,
                Money.Create(140m, "USD").Value,
                Money.Create(25m, "USD").Value)
            .Value);

        DateOnly effectiveCheckIn =
            checkInDate ??
            new DateOnly(2025, 6, 20);

        DateOnly effectiveCheckOut =
            checkOutDate ??
            effectiveCheckIn.AddDays(2);

        StayPeriod stayPeriod =
            StayPeriod.Create(
                effectiveCheckIn,
                effectiveCheckOut)
            .Value;

        DomainBooking booking =
            BookingTestData.CreateBooking(
                rentableUnit,
                stayPeriod,
                createdAtUtc:
                    CreatedAtUtc,
                approvalDueAtUtc:
                    approvalDueAtUtc ??
                    NowUtc.AddDays(10),
                customerSubjectId:
                    TestIdentitySubjects.Customer);

        if (status is
            BookingStatus.PendingPayment or
            BookingStatus.Paid)
        {
            var approvalResult =
                booking.Approve(
                    ApprovedAtUtc,
                    paymentDueAtUtc ??
                    NowUtc.AddHours(4));

            if (approvalResult.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Could not approve test booking: " +
                    $"{approvalResult.Error.Code}.");
            }
        }

        if (status == BookingStatus.Paid)
        {
            var paidResult =
                booking.MarkAsPaid(
                    ApprovedAtUtc.AddMinutes(10));

            if (paidResult.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Could not mark test booking as paid: " +
                    $"{paidResult.Error.Code}.");
            }
        }

        using IServiceScope scope =
            _factory.Services.CreateScope();

        IPropertyRepository propertyRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IPropertyRepository>();

        IRentableUnitRepository rentableUnitRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IRentableUnitRepository>();

        IBookingRepository bookingRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingRepository>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        propertyRepository.Add(property);
        rentableUnitRepository.Add(rentableUnit);
        bookingRepository.Add(booking);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return booking;
    }

    private sealed record IndexRow(
        string IndexName,
        string IndexDefinition);
}
