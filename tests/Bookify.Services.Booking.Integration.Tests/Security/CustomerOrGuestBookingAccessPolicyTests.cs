using System.Net;
using Bookify.Services.Booking.Domain.Bookings;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Security;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class CustomerOrGuestBookingAccessPolicyTests
{
    private readonly BookingApiFactory _factory;

    public CustomerOrGuestBookingAccessPolicyTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Cancel_WithBookingCustomer_ShouldSucceed()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory,
            BookingStatus.PendingApproval,
            customerSubjectId: TestIdentitySubjects.Customer,
            cancellationToken: cancellationToken);

        using HttpClient client = _factory.CreateCustomerClient();

        using HttpResponseMessage response = await client.PostAsync(
                $"/api/v1/bookings/{booking.Id}/cancel",
                content: null,
                cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_WithDifferentCustomer_ShouldReturnForbidden()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DomainBooking booking = await BookingDatabaseTestSeeder.SeedBookingAsync(
            _factory,
            BookingStatus.PendingApproval,
            customerSubjectId: "different-customer",
            cancellationToken: cancellationToken);

        using HttpClient client = _factory.CreateCustomerClient();

        using HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/bookings/{booking.Id}/cancel",
            content: null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_WithValidGuestCredential_ShouldSucceed()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        GuestBookingSeedData guest = await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
            _factory,
            BookingStatus.PendingApproval,
            cancellationToken: cancellationToken);

        using HttpClient client = _factory.CreateGuestClient(guest.GuestAccessToken);

        using HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/bookings/{guest.Booking.Id}/cancel",
            content: null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_WithInvalidGuestCredential_ShouldReturnUnauthorized()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        GuestBookingSeedData guest = await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
            _factory,
            BookingStatus.PendingApproval,
            cancellationToken: cancellationToken);

        using HttpClient client = _factory.CreateGuestClient(new string('x', 43));

        using HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/bookings/{guest.Booking.Id}/cancel",
            content: null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_WithoutJwtOrGuestCredential_ShouldReturnUnauthorized()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        GuestBookingSeedData guest = await BookingDatabaseTestSeeder.SeedGuestBookingAsync(
            _factory,
            BookingStatus.PendingApproval,
            cancellationToken: cancellationToken);

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/bookings/{guest.Booking.Id}/cancel",
            content: null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
