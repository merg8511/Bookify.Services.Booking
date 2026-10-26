
using System.Net;
using System.Net.Http.Json;
using Bookify.Services.Booking.Api.Contracts.Pagination;
using Bookify.Services.Booking.Api.Endpoints.Administrative.Bookings;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Endpoints.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class AdminBookingFilterBoundaryTests
{
    private readonly BookingApiFactory _factory;

    private static CancellationToken CancellationToken =>
        TestContext.Current.CancellationToken;

    public AdminBookingFilterBoundaryTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CombinedFilters_ShouldRespectStatusCustomerAndDateBoundaries()
    {
        // ARRANGE
        BookingScenario scenario =
            await SeedScenarioAsync();

        using HttpClient owner =
            _factory.CreateOwnerClient();

        string baseUrl =
            $"/api/v1/admin/properties/{scenario.PropertyId}/bookings";

        string customer = Uri.EscapeDataString(
            TestIdentitySubjects.Customer);

        string matchingUrl =
            $"{baseUrl}?status=PendingPayment" +
            "&rangeStart=2026-09-12" +
            "&rangeEnd=2026-09-15" +
            $"&unitId={scenario.UnitId}" +
            $"&customerSubjectId={customer}";

        // ACT — All filters should match exactly the second booking.
        PagedResponse<AdminBookingListItemResponse> matching =
            await GetPageAsync(owner, matchingUrl);

        // ASSERT
        Assert.Equal(1, matching.TotalRecords);
        Assert.Equal(1, matching.TotalPages);

        AdminBookingListItemResponse item =
            Assert.Single(matching.Items);

        Assert.Equal(
            scenario.SecondBookingId,
            item.Id);

        Assert.Equal(
            "PendingPayment",
            item.Status);

        Assert.Equal(
            TestIdentitySubjects.Customer,
            item.CustomerSubjectId);

        // ACT — The first Booking checks out exactly
        // at RangeStart and must not be included.
        PagedResponse<AdminBookingListItemResponse> startBoundary =
            await GetPageAsync(
                owner,
                $"{baseUrl}?rangeStart=2026-09-12" +
                "&rangeEnd=2026-09-14");

        // ASSERT
        Assert.DoesNotContain(
            startBoundary.Items,
            booking => booking.Id == scenario.FirstBookingId);

        Assert.Equal(
            scenario.SecondBookingId,
            Assert.Single(startBoundary.Items).Id);

        // ACT — No Booking overlaps this gap.
        PagedResponse<AdminBookingListItemResponse> emptyRange =
            await GetPageAsync(
                owner,
                $"{baseUrl}?rangeStart=2026-09-14" +
                "&rangeEnd=2026-09-15");

        // ASSERT — Empty pages are valid, not 404.
        Assert.Empty(emptyRange.Items);
        Assert.Equal(0, emptyRange.TotalRecords);
        Assert.Equal(0, emptyRange.TotalPages);

        // ACT — Guest checkout Booking has no customer subject.
        PagedResponse<AdminBookingListItemResponse> cancelled =
            await GetPageAsync(
                owner,
                $"{baseUrl}?status=Cancelled");

        // ASSERT
        AdminBookingListItemResponse guestBooking =
            Assert.Single(cancelled.Items);

        Assert.Equal(scenario.ThirdBookingId, guestBooking.Id);
        Assert.Null(guestBooking.CustomerSubjectId);
    }

    [Fact]
    public async Task Pagination_ShouldReportCorrectTotalsWithoutDuplicateItems()
    {
        // ARRANGE
        BookingScenario scenario =
            await SeedScenarioAsync();

        using HttpClient admin =
            _factory.CreateAdminClient();

        string baseUrl =
            $"/api/v1/admin/properties/{scenario.PropertyId}/bookings";

        // ACT
        PagedResponse<AdminBookingListItemResponse> firstPage =
            await GetPageAsync(
                admin,
                $"{baseUrl}?pageNumber=1&pageSize=1");

        PagedResponse<AdminBookingListItemResponse> secondPage =
            await GetPageAsync(
                admin,
                $"{baseUrl}?pageNumber=2&pageSize=1");

        PagedResponse<AdminBookingListItemResponse> thirdPage =
            await GetPageAsync(
                admin,
                $"{baseUrl}?pageNumber=3&pageSize=1");

        PagedResponse<AdminBookingListItemResponse> outsidePage =
            await GetPageAsync(
                admin,
                $"{baseUrl}?pageNumber=4&pageSize=1");

        // ASSERT — All pages share the same total.
        Assert.Equal(3, firstPage.TotalRecords);
        Assert.Equal(3, firstPage.TotalPages);

        Assert.Equal(3, secondPage.TotalRecords);
        Assert.Equal(3, secondPage.TotalPages);

        Assert.Equal(3, thirdPage.TotalRecords);
        Assert.Equal(3, thirdPage.TotalPages);

        // No fourth item exists.
        Assert.Empty(outsidePage.Items);
        Assert.Equal(3, outsidePage.TotalRecords);

        Guid[] actualIds =
        [
            Assert.Single(firstPage.Items).Id,
            Assert.Single(secondPage.Items).Id,
            Assert.Single(thirdPage.Items).Id
        ];

        Guid[] expectedIds =
        [
            scenario.FirstBookingId,
            scenario.SecondBookingId,
            scenario.ThirdBookingId
        ];

        // Pages must cover all three Bookings without duplicates.
        Assert.Equal(3, actualIds.Distinct().Count());

        Assert.Equal(
            expectedIds.OrderBy(id => id),
            actualIds.OrderBy(id => id));
    }

    private static async Task<
        PagedResponse<AdminBookingListItemResponse>> GetPageAsync(
            HttpClient client,
            string url)
    {
        using HttpResponseMessage response =
            await client.GetAsync(url, CancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        return Assert.IsType<
            PagedResponse<AdminBookingListItemResponse>>(
            await response.Content.ReadFromJsonAsync<
                PagedResponse<AdminBookingListItemResponse>>(
                CancellationToken));
    }

    private async Task<BookingScenario> SeedScenarioAsync()
    {
        // One property and one rentable unit for all three Bookings.
        SeedData seed =
            await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
                _factory,
                cancellationToken: CancellationToken);

        // Booking 1: [Sep 10, Sep 12), authenticated customer.
        DomainBooking first =
            BookingTestData.CreateBooking(
                seed.RentableUnit,
                StayPeriod.Create(
                    new DateOnly(2026, 9, 10),
                    new DateOnly(2026, 9, 12)).Value,
                customerSubjectId: TestIdentitySubjects.Customer);

        // Booking 2: [Sep 12, Sep 14), authenticated customer.
        DomainBooking second =
            BookingTestData.CreateBooking(
                seed.RentableUnit,
                StayPeriod.Create(
                    new DateOnly(2026, 9, 12),
                    new DateOnly(2026, 9, 14)).Value,
                customerSubjectId: TestIdentitySubjects.Customer);

        // Transition to PendingPayment without invoking Stripe.
        Assert.True(
            second.Approve(
                BookingTestTime.ApprovedAtUtc,
                BookingTestTime.PaymentDueAtUtc).IsSuccess);

        // Booking 3: [Sep 15, Sep 18), Guest checkout.
        DomainBooking third =
            BookingTestData.CreateBooking(
                seed.RentableUnit,
                StayPeriod.Create(
                    new DateOnly(2026, 9, 15),
                    new DateOnly(2026, 9, 18)).Value,
                customerSubjectId: null);

        Assert.True(
            third.Cancel(
                BookingTestTime.CancelledAtUtc).IsSuccess);

        using IServiceScope scope =
            _factory.Services.CreateScope();

        IBookingRepository repository =
            scope.ServiceProvider
                .GetRequiredService<IBookingRepository>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<IUnitOfWork>();

        repository.Add(first);
        repository.Add(second);
        repository.Add(third);

        await unitOfWork.SaveChangesAsync(CancellationToken);

        return new BookingScenario(
            seed.PropertyId,
            seed.RentableUnitId,
            first.Id,
            second.Id,
            third.Id);
    }

    private sealed record BookingScenario(
        Guid PropertyId,
        Guid UnitId,
        Guid FirstBookingId,
        Guid SecondBookingId,
        Guid ThirdBookingId);
}
