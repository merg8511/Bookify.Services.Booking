
using System.Data.Common;
using System.Text.Json;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Application.Outbox;
using Bookify.Services.Booking.Domain.Bookings.ValueObjects;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using DomainBooking = Bookify.Services.Booking.Domain.Bookings.Booking;

namespace Bookify.Services.Booking.Integration.Tests.Outbox;

[Collection(BookingApiTestFixture.Name)]
[Trait("Category", "Integration")]
public sealed class OutboxPersistenceIntegrationTests
{
    private readonly BookingApiFactory _factory;

    public OutboxPersistenceIntegrationTests(
        BookingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Commit_ShouldPersistBookingAndOutboxMessageOnlyOnce()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        SeedData data = await BookingDatabaseTestSeeder
            .SeedPropertyWithRoomAsync(
                _factory.Services,
                cancellationToken: cancellationToken);

        DomainBooking booking = CreateBooking(data);

        using (IServiceScope scope =
            _factory.Services.CreateScope())
        {
            IBookingRepository repository =
                scope.ServiceProvider
                    .GetRequiredService<IBookingRepository>();

            IUnitOfWork unitOfWork =
                scope.ServiceProvider
                    .GetRequiredService<IUnitOfWork>();

            ITransactionManager transactionManager =
                scope.ServiceProvider
                    .GetRequiredService<ITransactionManager>();

            await using ITransaction transaction =
                await transactionManager.BeginAsync(
                    cancellationToken);

            repository.Add(booking);

            // First SaveChanges stages and saves the event.
            await unitOfWork.SaveChangesAsync(
                cancellationToken);

            // Second SaveChanges must not duplicate the event.
            await unitOfWork.SaveChangesAsync(
                cancellationToken);

            // Domain Events must remain pending until commit.
            Assert.Single(booking.GetDomainEvents());

            await transaction.CommitAsync(
                cancellationToken);
        }

        // ASSERT
        Assert.Empty(booking.GetDomainEvents());

        using IServiceScope verificationScope =
            _factory.Services.CreateScope();

        BookingDbContext dbContext =
            verificationScope.ServiceProvider
                .GetRequiredService<BookingDbContext>();

        bool bookingExists = await dbContext.Bookings
            .AsNoTracking()
            .AnyAsync(
                current => current.Id == booking.Id,
                cancellationToken);

        Assert.True(bookingExists);

        IOutboxMessageReader outboxReader =
            verificationScope.ServiceProvider
                .GetRequiredService<IOutboxMessageReader>();

        IReadOnlyList<PendingOutboxMessageReadModel> messages =
            await outboxReader.GetPendingMessagesAsync(
                10000,
                cancellationToken);

        PendingOutboxMessageReadModel[] matchingMessages =
            messages
                .Where(message =>
                    message.EventType ==
                        "BookingCreatedDomainEvent" &&
                    GetBookingId(message.Payload) == booking.Id)
                .ToArray();

        Assert.Single(matchingMessages);
        Assert.NotEqual(Guid.Empty, matchingMessages[0].Id);
    }

    [Fact]
    public async Task Rollback_ShouldNotPersistBookingOrOutboxMessage()
    {
        // ARRANGE
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        SeedData data = await BookingDatabaseTestSeeder
            .SeedPropertyWithRoomAsync(
                _factory.Services,
                cancellationToken: cancellationToken);

        DomainBooking booking = CreateBooking(data);

        using (IServiceScope scope =
            _factory.Services.CreateScope())
        {
            IBookingRepository repository =
                scope.ServiceProvider
                    .GetRequiredService<IBookingRepository>();

            IUnitOfWork unitOfWork =
                scope.ServiceProvider
                    .GetRequiredService<IUnitOfWork>();

            ITransactionManager transactionManager =
                scope.ServiceProvider
                    .GetRequiredService<ITransactionManager>();

            await using ITransaction transaction =
                await transactionManager.BeginAsync(
                    cancellationToken);

            repository.Add(booking);

            await unitOfWork.SaveChangesAsync(
                cancellationToken);

            Assert.Single(booking.GetDomainEvents());

            // ACT
            await transaction.RollbackAsync(
                cancellationToken);
        }

        Assert.Empty(booking.GetDomainEvents());

        // ASSERT
        using IServiceScope verificationScope =
            _factory.Services.CreateScope();

        BookingDbContext dbContext =
            verificationScope.ServiceProvider
                .GetRequiredService<BookingDbContext>();

        bool bookingExists = await dbContext.Bookings
            .AsNoTracking()
            .AnyAsync(
                current => current.Id == booking.Id,
                cancellationToken);

        Assert.False(bookingExists);

        IDbConnectionFactory connectionFactory =
            verificationScope.ServiceProvider
                .GetRequiredService<IDbConnectionFactory>();

        await using DbConnection connection =
            await connectionFactory.OpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            """
            SELECT COUNT(*)
            FROM outbox_messages
            WHERE payload ->> 'bookingId' = @BookingId;
            """,
            new
            {
                BookingId = booking.Id.ToString("D")
            },
            cancellationToken: cancellationToken);

        long outboxCount =
            await connection.QuerySingleAsync<long>(
                command);

        Assert.Equal(0, outboxCount);
    }

    private static DomainBooking CreateBooking(
        SeedData data)
    {
        StayPeriod stayPeriod = StayPeriod.Create(
            new DateOnly(2037, 1, 10),
            new DateOnly(2037, 1, 12)).Value;

        return BookingTestData.CreateBooking(
            data.RentableUnit,
            stayPeriod,
            customerSubjectId:
                TestIdentitySubjects.Customer);
    }

    private static Guid GetBookingId(string payload)
    {
        using JsonDocument document =
            JsonDocument.Parse(payload);

        return document.RootElement
            .GetProperty("bookingId")
            .GetGuid();
    }
}
