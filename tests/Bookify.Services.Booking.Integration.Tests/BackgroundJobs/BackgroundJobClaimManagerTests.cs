using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.BackgroundJobs;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "Concurrency")]
public sealed class BackgroundJobClaimManagerTests
{
    private readonly BookingApiFactory _factory;

    public BackgroundJobClaimManagerTests(
        BookingApiFactory factory)
    {
        _factory = factory
            ?? throw new ArgumentNullException(
                nameof(factory));
    }

    [Fact]
    public async Task TryAcquireAsync_WhenTwoInstancesCompeteForSameWorkItem_ShouldAllowExactlyOneClaim()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid workItemId =
            Guid.NewGuid();

        using IServiceScope firstScope =
            _factory.Services.CreateScope();

        using IServiceScope secondScope =
            _factory.Services.CreateScope();

        IBackgroundJobClaimManager firstManager =
            firstScope.ServiceProvider
                .GetRequiredService<
                    IBackgroundJobClaimManager>();

        IBackgroundJobClaimManager secondManager =
            secondScope.ServiceProvider
                .GetRequiredService<
                    IBackgroundJobClaimManager>();

        // ACT
        Task<IBackgroundJobClaim?> firstTask =
            firstManager.TryAcquireAsync(
                BackgroundJobClaimKeys
                    .OutboxNotification,
                workItemId,
                cancellationToken);

        Task<IBackgroundJobClaim?> secondTask =
            secondManager.TryAcquireAsync(
                BackgroundJobClaimKeys
                    .OutboxNotification,
                workItemId,
                cancellationToken);

        IBackgroundJobClaim?[] claims =
            await Task.WhenAll(
                firstTask,
                secondTask);

        // ASSERT
        IBackgroundJobClaim acquiredClaim =
            Assert.Single(
                claims,
                claim => claim is not null)!;

        Assert.Single(
            claims,
            claim => claim is null);

        // The winning session owns the claim until
        // its lease is disposed.
        await acquiredClaim.DisposeAsync();

        // After release, the same work item becomes
        // available to another worker.
        IBackgroundJobClaim? reacquiredClaim =
            await secondManager.TryAcquireAsync(
                BackgroundJobClaimKeys
                    .OutboxNotification,
                workItemId,
                cancellationToken);

        Assert.NotNull(
            reacquiredClaim);

        await reacquiredClaim.DisposeAsync();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenSameWorkItemUsesDifferentJobKeys_ShouldAllowBothClaims()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid bookingId =
            Guid.NewGuid();

        using IServiceScope firstScope =
            _factory.Services.CreateScope();

        using IServiceScope secondScope =
            _factory.Services.CreateScope();

        IBackgroundJobClaimManager firstManager =
            firstScope.ServiceProvider
                .GetRequiredService<
                    IBackgroundJobClaimManager>();

        IBackgroundJobClaimManager secondManager =
            secondScope.ServiceProvider
                .GetRequiredService<
                    IBackgroundJobClaimManager>();

        // ACT
        IBackgroundJobClaim? expirationClaim =
            await firstManager.TryAcquireAsync(
                BackgroundJobClaimKeys
                    .PaymentExpiration,
                bookingId,
                cancellationToken);

        IBackgroundJobClaim? reminderClaim =
            await secondManager.TryAcquireAsync(
                BackgroundJobClaimKeys
                    .PaymentDeadlineReminder,
                bookingId,
                cancellationToken);

        try
        {
            // ASSERT
            Assert.NotNull(
                expirationClaim);

            Assert.NotNull(
                reminderClaim);
        }
        finally
        {
            if (expirationClaim is not null)
            {
                await expirationClaim.DisposeAsync();
            }

            if (reminderClaim is not null)
            {
                await reminderClaim.DisposeAsync();
            }
        }
    }
}
