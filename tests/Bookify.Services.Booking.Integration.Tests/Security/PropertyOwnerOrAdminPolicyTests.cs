using System.Net;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Security;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class PropertyOwnerOrAdminPolicyTests
{
    private readonly BookingApiFactory _factory;

    public PropertyOwnerOrAdminPolicyTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Approve_WithPropertyOwner_ShouldSucceed()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory,
            BookingStatus.PendingApproval,
            ownerSubjectId: TestIdentitySubjects.Owner,
            cancellationToken: cancellationToken);

        using HttpClient client = _factory.CreateOwnerClient();

        using HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/bookings/{booking.Id}/approve",
            content: null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Approve_WithDifferentOwner_ShouldReturnForbidden()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking = await BookingDatabaseTestSeeder
            .SeedBookingAsync(
            _factory,
            BookingStatus.PendingApproval,
            ownerSubjectId: "another-owner-subject",
            cancellationToken: cancellationToken);

        using HttpClient client = _factory.CreateOwnerClient();

        using HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/bookings/{booking.Id}/approve",
            content: null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Approve_WithAdmin_ShouldSucceed()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory,
            BookingStatus.PendingApproval,
            ownerSubjectId: "some-other-owner",
            cancellationToken: cancellationToken);

        using HttpClient client = _factory.CreateAdminClient();

        using HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/bookings/{booking.Id}/approve",
            content: null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
