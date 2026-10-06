using Bookify.Services.Booking.Domain.Bookings.Errors;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Tests.Infrastructure;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Domain.Tests.Bookings;

public sealed class BookingOwnershipTests
{
    [Fact]
    public void Create_WithoutCustomerSubject_ShouldCreateGuestCredential()
    {
        Result<DomainBooking> result = CreateBooking();

        Assert.True(result.IsSuccess);

        DomainBooking booking = result.Value;

        Assert.Null(booking.CustomerSubjectId);
        Assert.NotNull(booking.GuestAccessTokenHash);

        string token = Assert.IsType<string>(booking.TakeGuestAccessToken());

        Assert.Equal(43, token.Length);
        Assert.Equal(64, booking.GuestAccessTokenHash.Length);
        Assert.NotEqual(token, booking.GuestAccessTokenHash);

        Assert.True(booking.VerifyGuestAccessToken(token));
        Assert.False(booking.VerifyGuestAccessToken(token + "x"));
        Assert.False(booking.VerifyGuestAccessToken(null));

        Assert.Null(booking.TakeGuestAccessToken());

        // The persisted hash remains usable after issuance.
        Assert.True(booking.VerifyGuestAccessToken(token));
    }

    [Fact]
    public void Create_TwoGuestBookings_ShouldGenerateDifferentCredentials()
    {
        DomainBooking first = CreateBooking().Value;
        DomainBooking second = CreateBooking().Value;

        string firstToken = Assert.IsType<string>(first.TakeGuestAccessToken());
        string secondToken = Assert.IsType<string>(second.TakeGuestAccessToken());

        Assert.NotEqual(firstToken, secondToken);
        Assert.NotEqual(first.GuestAccessTokenHash, second.GuestAccessTokenHash);
        Assert.False(first.VerifyGuestAccessToken(secondToken));
    }

    [Fact]
    public void Create_WithCustomerSubject_ShouldNotGenerateGuestCredential()
    {
        DomainBooking booking = CreateBooking("customer-123").Value;

        Assert.Equal("customer-123", booking.CustomerSubjectId);
        Assert.Null(booking.GuestAccessTokenHash);
        Assert.Null(booking.TakeGuestAccessToken());
        Assert.False(booking.VerifyGuestAccessToken("anything"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" customer-123 ")]
    public void Create_WithInvalidCustomerSubject_ShouldFail(string subject)
    {
        Result<DomainBooking> result = CreateBooking(subject);

        Assert.True(result.IsFailure);
        Assert.Equal(BookingErrors.InvalidCustomerSubjectId, result.Error);
    }

    [Fact]
    public void Create_WithCustomerSubjectAboveMaximum_ShouldFail()
    {
        Result<DomainBooking> result = CreateBooking(new string('a', 256));

        Assert.True(result.IsFailure);
        Assert.Equal(BookingErrors.InvalidCustomerSubjectId, result.Error);
    }

    private static Result<DomainBooking> CreateBooking(string? customerSubjectId = null)
    {
        RentableUnit unit = RentableUnitTestFactory.CreateValidRentableUnit(name: "Room A");
        var stay = BookingTestData.CreateStayPeriod(10, 12, month: 10, year: 2026);

        return DomainBooking.Create(
            unit,
            stay,
            BookingTestData.CreateGuestCount(),
            BookingTestData.CreateGuestDetails(),
            BookingTestData.CreatePriceSnapshot(),
            BookingTestTime.CreatedAtUtc,
            BookingTestTime.ApprovalDueAtUtc,
            customerSubjectId);
    }
}
