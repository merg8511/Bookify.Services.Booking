using Bookify.Services.Booking.Application;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Bookings;
using Bookify.Services.Booking.Application.Bookings.Approve;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Domain.Properties;
using Bookify.Services.Booking.Domain.Properties.Pricing;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Domain.Shared.ValueObjects;
using Bookify.Services.Booking.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.IntegrationTests.Bookings;

[Collection(
    BookingApiTestFixture.Name)]
[Trait(
    "Category",
    "Integration")]
public sealed class
    BookingDeadlineFlowIntegrationTests
{
    private readonly BookingApiFactory _factory;

    public BookingDeadlineFlowIntegrationTests(
        BookingApiFactory factory)
    {
        _factory =
            factory;
    }

    [Fact]
    public async Task
        CreateAndApprove_ShouldPersistConfiguredDeadlines()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current
                .CancellationToken;

        SeedData data =
            await SeedPropertyWithRoomAsync(
                cancellationToken);

        var createCommand =
            new CreateBookingCommand(
                data.PropertyId,
                data.RentableUnitId,
                new DateOnly(
                    2026,
                    11,
                    10),
                new DateOnly(
                    2026,
                    11,
                    12),
                GuestCount: 2,
                GuestFullName:
                    "John Doe",
                GuestEmail:
                    "john@example.com",
                GuestPhone:
                    "+50377778888");

        Result<CreateBookingResult>
            createResult;

        using (
            IServiceScope createScope =
                _factory.Services
                    .CreateScope())
        {
            ICommandExecutor<
                CreateBookingCommand,
                CreateBookingResult> executor =
                    createScope.ServiceProvider
                        .GetRequiredService<
                            ICommandExecutor<
                                CreateBookingCommand,
                                CreateBookingResult>>();

            createResult =
                await executor.ExecuteAsync(
                    createCommand,
                    cancellationToken);
        }

        Assert.True(
            createResult.IsSuccess);

        Guid bookingId =
            createResult.Value.Id;

        // ASSERT CREATE DEADLINE
        BookingDetailsReadModel
            afterCreation =
                await GetBookingAsync(
                    bookingId,
                    cancellationToken);

        Assert.NotNull(
            afterCreation.CreatedAtUtc);

        Assert.NotNull(
            afterCreation.ApprovalDueAtUtc);

        Assert.Equal(
            TimeSpan.FromHours(24),
            afterCreation
                .ApprovalDueAtUtc!
                .Value
                .Subtract(
                    afterCreation
                        .CreatedAtUtc!
                        .Value));

        Assert.Null(
            afterCreation.ApprovedAtUtc);

        Assert.Null(
            afterCreation.PaymentDueAtUtc);

        // ACT APPROVE
        using (
            IServiceScope approveScope =
                _factory.Services
                    .CreateScope())
        {
            ICommandExecutor<
                ApproveBookingCommand> executor =
                    approveScope.ServiceProvider
                        .GetRequiredService<
                            ICommandExecutor<
                                ApproveBookingCommand>>();

            Result approvalResult =
                await executor.ExecuteAsync(
                    new ApproveBookingCommand(
                        bookingId),
                    cancellationToken);

            Assert.True(
                approvalResult.IsSuccess);
        }

        // ASSERT PAYMENT DEADLINE
        BookingDetailsReadModel
            afterApproval =
                await GetBookingAsync(
                    bookingId,
                    cancellationToken);

        Assert.NotNull(
            afterApproval.ApprovedAtUtc);

        Assert.NotNull(
            afterApproval.PaymentDueAtUtc);

        Assert.Equal(
            TimeSpan.FromMinutes(30),
            afterApproval
                .PaymentDueAtUtc!
                .Value
                .Subtract(
                    afterApproval
                        .ApprovedAtUtc!
                        .Value));

        Assert.Equal(
            afterCreation.ApprovalDueAtUtc,
            afterApproval.ApprovalDueAtUtc);
    }

    private async Task<BookingDetailsReadModel>
        GetBookingAsync(
            Guid bookingId,
            CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IBookingReadService readService =
            scope.ServiceProvider
                .GetRequiredService<
                    IBookingReadService>();

        BookingDetailsReadModel? booking =
            await readService.GetByIdAsync(
                bookingId,
                cancellationToken);

        return Assert.IsType<
            BookingDetailsReadModel>(
                booking);
    }

    private async Task<SeedData>
        SeedPropertyWithRoomAsync(
            CancellationToken cancellationToken)
    {
        Property property =
            Property.Create(
                    $"Deadline Test " +
                    $"{Guid.NewGuid():N}",
                    "America/El_Salvador",
                    new TimeOnly(
                        15,
                        0),
                    new TimeOnly(
                        11,
                        0))
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
                    Money.Create(
                            100m,
                            "USD")
                        .Value,
                    Money.Create(
                            140m,
                            "USD")
                        .Value,
                    Money.Create(
                            25m,
                            "USD")
                        .Value)
                .Value);

        using IServiceScope scope =
            _factory.Services
                .CreateScope();

        IPropertyRepository propertyRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IPropertyRepository>();

        IRentableUnitRepository rentableUnitRepository =
            scope.ServiceProvider
                .GetRequiredService<
                    IRentableUnitRepository>();

        IUnitOfWork unitOfWork =
            scope.ServiceProvider
                .GetRequiredService<
                    IUnitOfWork>();

        propertyRepository.Add(
            property);

        rentableUnitRepository.Add(
            rentableUnit);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return new SeedData(
            property.Id,
            rentableUnit.Id);
    }

    private sealed record SeedData(
        Guid PropertyId,
        Guid RentableUnitId);
}
