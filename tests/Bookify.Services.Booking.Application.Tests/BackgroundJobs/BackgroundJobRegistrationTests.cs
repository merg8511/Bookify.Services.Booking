using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Application.Tests.BackgroundJobs;

public sealed class BackgroundJobRegistrationTests
{
    [Fact]
    public void AddApplication_ShouldRegisterEachBackgroundJobExactlyOnce()
    {
        // ARRANGE
        var services =
            new ServiceCollection();

        // ACT
        services.AddApplication();

        ServiceDescriptor[] jobDescriptors =
            services
                .Where(
                    descriptor =>
                        descriptor.ServiceType ==
                        typeof(IBackgroundJob))
                .ToArray();

        // ASSERT
        Assert.Equal(
            6,
            jobDescriptors.Length);

        object?[] keys =
            jobDescriptors
                .Select(
                    descriptor =>
                        descriptor.ServiceKey)
                .ToArray();

        Assert.Equal(
            keys.Length,
            keys.Distinct().Count());

        Assert.Contains(
            BackgroundJobClaimKeys.ApprovalExpiration,
            keys);

        Assert.Contains(
            BackgroundJobClaimKeys.PaymentExpiration,
            keys);

        Assert.Contains(
            BackgroundJobClaimKeys.BookingCompletion,
            keys);

        Assert.Contains(
            BackgroundJobClaimKeys.OutboxNotification,
            keys);

        Assert.Contains(
            BackgroundJobClaimKeys.PaymentDeadlineReminder,
            keys);

        Assert.Contains(
            BackgroundJobClaimKeys.UpcomingCheckInReminder,
            keys);
    }
}
