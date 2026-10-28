
using Bookify.Services.Booking.Domain.Tests.Infrastructure;

using DomainBooking =
    Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Domain.Tests.Bookings;

public sealed class BookingGuestCredentialInvariantTests
{
    [Fact]
    public void VerifyGuestAccessToken_WithAlteredValidLength_ShouldRejectToken()
    {
        // ARRANGE
        DomainBooking booking =
            BookingTestFactory.CreateValidBooking();

        string originalToken = Assert.IsType<string>(
            booking.TakeGuestAccessToken());

        string originalHash = Assert.IsType<string>(
            booking.GuestAccessTokenHash);

        char[] alteredCharacters = originalToken.ToCharArray();

        alteredCharacters[10] =
            alteredCharacters[10] == 'A' ? 'B' : 'A';

        string alteredToken = new(alteredCharacters);

        // ACT
        bool alteredIsValid =
            booking.VerifyGuestAccessToken(alteredToken);

        bool originalIsValid =
            booking.VerifyGuestAccessToken(originalToken);

        // ASSERT
        Assert.Equal(43, alteredToken.Length);
        Assert.NotEqual(originalToken, alteredToken);

        Assert.False(alteredIsValid);
        Assert.True(originalIsValid);

        Assert.Equal(originalHash, booking.GuestAccessTokenHash);
    }

    [Fact]
    public void Create_WithMaximumCustomerSubjectLength_ShouldNotGenerateGuestCredential()
    {
        // ARRANGE
        string customerSubjectId = new('x', 255);

        // ACT
        DomainBooking booking =
            BookingTestFactory.CreateValidBooking(
                customerSubjectId: customerSubjectId);

        // ASSERT
        Assert.Equal(customerSubjectId, booking.CustomerSubjectId);
        Assert.Null(booking.GuestAccessTokenHash);
        Assert.Null(booking.TakeGuestAccessToken());

        Assert.False(
            booking.VerifyGuestAccessToken(
                new string('A', 43)));
    }
}
