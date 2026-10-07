using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.BackgroundJobs;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.BackgroundJobs;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class PendingBookingWorkSelectorTests
{
    private const int BatchSize = 100;

    private readonly BookingApiFactory _factory;

    public PendingBookingWorkSelectorTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Selector_ShouldReturnBookingsMatchingPendingWorkWindows()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        SeedData data =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory.Services,
                propertyName: $"Pending Work {Guid.NewGuid():N}",
                cancellationToken: cancellationToken);

        DomainBooking expiredApprovalBooking =
            CreatePendingApprovalBooking(
                data,
                createdAtUtc:
                    new DateTimeOffset(
                        2036,
                        12,
                        30,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                approvalDueAtUtc:
                    new DateTimeOffset(
                        2037,
                        1,
                        1,
                        0,
                        0,
                        0,
                        TimeSpan.Zero),
                checkInDate:
                    new DateOnly(2037, 1, 10),
                checkOutDate:
                    new DateOnly(2037, 1, 12));

        DomainBooking expiredPaymentBooking =
            CreatePendingPaymentBooking(
                data,
                createdAtUtc:
                    new DateTimeOffset(
                        2037,
                        1,
                        30,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                approvedAtUtc:
                    new DateTimeOffset(
                        2037,
                        1,
                        31,
                        0,
                        0,
                        0,
                        TimeSpan.Zero),
                paymentDueAtUtc:
                    new DateTimeOffset(
                        2037,
                        2,
                        1,
                        0,
                        0,
                        0,
                        TimeSpan.Zero),
                checkInDate:
                    new DateOnly(2037, 2, 10),
                checkOutDate:
                    new DateOnly(2037, 2, 12));

        DomainBooking completionBooking =
            CreatePaidBooking(
                data,
                createdAtUtc:
                    new DateTimeOffset(
                        2037,
                        3,
                        1,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                checkInDate:
                    new DateOnly(2037, 3, 8),
                checkOutDate:
                    new DateOnly(2037, 3, 10));

        DomainBooking checkInReminderBooking =
            CreatePaidBooking(
                data,
                createdAtUtc:
                    new DateTimeOffset(
                        2037,
                        4,
                        1,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                checkInDate:
                    new DateOnly(2037, 5, 1),
                checkOutDate:
                    new DateOnly(2037, 5, 2));

        using (IServiceScope saveScope =
            _factory.Services.CreateScope())
        {
            IBookingRepository bookingRepository =
                saveScope.ServiceProvider
                    .GetRequiredService<IBookingRepository>();

            IUnitOfWork unitOfWork =
                saveScope.ServiceProvider
                    .GetRequiredService<IUnitOfWork>();

            bookingRepository.Add(expiredApprovalBooking);
            bookingRepository.Add(expiredPaymentBooking);
            bookingRepository.Add(completionBooking);
            bookingRepository.Add(checkInReminderBooking);

            await unitOfWork.SaveChangesAsync(
                cancellationToken);
        }

        using IServiceScope selectorScope =
            _factory.Services.CreateScope();

        IPendingBookingWorkSelector selector =
            selectorScope.ServiceProvider
                .GetRequiredService<IPendingBookingWorkSelector>();

        // ACT
        IReadOnlyList<Guid> expiredApprovalIds =
            await selector.GetExpiredApprovalBookingIdsAsync(
                new DateTimeOffset(
                    2037,
                    1,
                    1,
                    0,
                    1,
                    0,
                    TimeSpan.Zero),
                BatchSize,
                cancellationToken);

        IReadOnlyList<Guid> expiredPaymentIds =
            await selector.GetExpiredPaymentBookingIdsAsync(
                new DateTimeOffset(
                    2037,
                    2,
                    1,
                    0,
                    1,
                    0,
                    TimeSpan.Zero),
                BatchSize,
                cancellationToken);

        IReadOnlyList<Guid> paymentReminderIds =
            await selector.GetPaymentDeadlineReminderCandidateIdsAsync(
                new DateTimeOffset(
                    2037,
                    1,
                    31,
                    23,
                    59,
                    0,
                    TimeSpan.Zero),
                new DateTimeOffset(
                    2037,
                    2,
                    1,
                    0,
                    1,
                    0,
                    TimeSpan.Zero),
                BatchSize,
                cancellationToken);

        // El Salvador is UTC-6.
        // Property checkout = 11:00 local => 17:00 UTC.
        IReadOnlyList<Guid> completionIds =
            await selector.GetDueCompletionBookingIdsAsync(
                new DateTimeOffset(
                    2037,
                    3,
                    10,
                    17,
                    0,
                    0,
                    TimeSpan.Zero),
                BatchSize,
                cancellationToken);

        // Property check-in = 15:00 local => 21:00 UTC.
        IReadOnlyList<Guid> checkInReminderIds =
            await selector.GetUpcomingCheckInReminderCandidateIdsAsync(
                new DateTimeOffset(
                    2037,
                    5,
                    1,
                    20,
                    59,
                    0,
                    TimeSpan.Zero),
                new DateTimeOffset(
                    2037,
                    5,
                    1,
                    21,
                    1,
                    0,
                    TimeSpan.Zero),
                BatchSize,
                cancellationToken);

        // ASSERT
        Assert.Contains(
            expiredApprovalBooking.Id,
            expiredApprovalIds);

        Assert.DoesNotContain(
            expiredPaymentBooking.Id,
            expiredApprovalIds);

        Assert.Contains(
            expiredPaymentBooking.Id,
            expiredPaymentIds);

        Assert.DoesNotContain(
            expiredApprovalBooking.Id,
            expiredPaymentIds);

        Assert.Contains(
            expiredPaymentBooking.Id,
            paymentReminderIds);

        Assert.Contains(
            completionBooking.Id,
            completionIds);

        Assert.DoesNotContain(
            checkInReminderBooking.Id,
            completionIds);

        Assert.Contains(
            checkInReminderBooking.Id,
            checkInReminderIds);
    }

    private static DomainBooking CreatePendingApprovalBooking(
        SeedData data,
        DateTimeOffset createdAtUtc,
        DateTimeOffset approvalDueAtUtc,
        DateOnly checkInDate,
        DateOnly checkOutDate)
    {
        StayPeriod stayPeriod =
            StayPeriod.Create(
                checkInDate,
                checkOutDate).Value;

        return BookingTestData.CreateBooking(
            data.RentableUnit,
            stayPeriod,
            createdAtUtc: createdAtUtc,
            approvalDueAtUtc: approvalDueAtUtc,
            customerSubjectId:
                TestIdentitySubjects.Customer);
    }

    private static DomainBooking CreatePendingPaymentBooking(
        SeedData data,
        DateTimeOffset createdAtUtc,
        DateTimeOffset approvedAtUtc,
        DateTimeOffset paymentDueAtUtc,
        DateOnly checkInDate,
        DateOnly checkOutDate)
    {
        DomainBooking booking =
            CreatePendingApprovalBooking(
                data,
                createdAtUtc,
                approvedAtUtc.AddHours(1),
                checkInDate,
                checkOutDate);

        var approvalResult =
            booking.Approve(
                approvedAtUtc,
                paymentDueAtUtc);

        Assert.True(approvalResult.IsSuccess);

        return booking;
    }

    private static DomainBooking CreatePaidBooking(
        SeedData data,
        DateTimeOffset createdAtUtc,
        DateOnly checkInDate,
        DateOnly checkOutDate)
    {
        DateTimeOffset approvedAtUtc =
            createdAtUtc.AddHours(1);

        DateTimeOffset paymentDueAtUtc =
            approvedAtUtc.AddHours(1);

        DomainBooking booking =
            CreatePendingPaymentBooking(
                data,
                createdAtUtc,
                approvedAtUtc,
                paymentDueAtUtc,
                checkInDate,
                checkOutDate);

        var paymentResult =
            booking.MarkAsPaid(
                approvedAtUtc.AddMinutes(30));

        Assert.True(paymentResult.IsSuccess);

        return booking;
    }
}
