using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.Approve;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.Bookings;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class BookingDeadlineFlowIntegrationTests
{
    private readonly BookingApiFactory _factory;

    public BookingDeadlineFlowIntegrationTests(BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateAndApprove_ShouldPersistConfiguredDeadlines()
    {
        // ARRANGE
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SeedData data = await BookingDatabaseTestSeeder.SeedPropertyWithRoomAsync(
            _factory.Services,
            cancellationToken: cancellationToken);

        var createCommand = new CreateBookingCommand(
            data.PropertyId,
            data.RentableUnitId,
            new DateOnly(2026, 11, 10),
            new DateOnly(2026, 11, 12),
            GuestCount: 2,
            GuestFullName: "John Doe",
            GuestEmail: "john@example.com",
            GuestPhone: "+50377778888");

        Result<CreateBookingResult> createResult;

        using (IServiceScope createScope = _factory.Services.CreateScope())
        {
            var executor = createScope.ServiceProvider
                .GetRequiredService<ICommandExecutor<CreateBookingCommand, CreateBookingResult>>();

            createResult = await executor.ExecuteAsync(createCommand, cancellationToken);
        }

        Assert.True(createResult.IsSuccess);
        Guid bookingId = createResult.Value.Id;

        // ASSERT CREATE DEADLINE
        BookingDetailsReadModel afterCreation = await GetBookingAsync(bookingId, cancellationToken);

        Assert.NotNull(afterCreation.CreatedAtUtc);
        Assert.NotNull(afterCreation.ApprovalDueAtUtc);
        Assert.Equal(TimeSpan.FromHours(24), afterCreation.ApprovalDueAtUtc!.Value.Subtract(afterCreation.CreatedAtUtc!.Value));
        Assert.Null(afterCreation.ApprovedAtUtc);
        Assert.Null(afterCreation.PaymentDueAtUtc);

        // ACT APPROVE
        using (IServiceScope approveScope = _factory.Services.CreateScope())
        {
            var executor = approveScope.ServiceProvider
                .GetRequiredService<ICommandExecutor<ApproveBookingCommand>>();

            Result approvalResult = await executor.ExecuteAsync(new ApproveBookingCommand(bookingId), cancellationToken);
            Assert.True(approvalResult.IsSuccess);
        }

        // ASSERT PAYMENT DEADLINE
        BookingDetailsReadModel afterApproval = await GetBookingAsync(bookingId, cancellationToken);

        Assert.NotNull(afterApproval.ApprovedAtUtc);
        Assert.NotNull(afterApproval.PaymentDueAtUtc);
        Assert.Equal(TimeSpan.FromMinutes(30), afterApproval.PaymentDueAtUtc!.Value.Subtract(afterApproval.ApprovedAtUtc!.Value));
        Assert.Equal(afterCreation.ApprovalDueAtUtc, afterApproval.ApprovalDueAtUtc);
    }

    private async Task<BookingDetailsReadModel> GetBookingAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IBookingReadService readService = scope.ServiceProvider.GetRequiredService<IBookingReadService>();

        BookingDetailsReadModel? booking = await readService.GetByIdAsync(bookingId, cancellationToken);
        return Assert.IsType<BookingDetailsReadModel>(booking);
    }
}
