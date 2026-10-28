
using System.Data.Common;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Properties.Create;
using Bookify.Services.Booking.Application.Properties.GetById;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Bookify.Services.Booking.Domain.Shared;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Bookify.Services.Booking.Integration.Tests.Infrastructure;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Integration.Tests.Migrations;

[Trait("Category", "Integration")]
[Trait("Category", "Migrations")]
public sealed class MigrationSmokeTests
{
    [Fact]
    public async Task EmptyDatabase_ShouldReachCurrentSchemaAndRemainIdempotent()
    {
        // ARRANGE — Un contenedor PostgreSQL completamente nuevo.
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using var database = new PostgreSqlTestDatabase();

        await database.StartAsync(cancellationToken);

        await using ServiceProvider services =
            IntegrationTestServiceProvider.Create(
                database.ConnectionString);

        IDbConnectionFactory connectionFactory =
            services.GetRequiredService<IDbConnectionFactory>();

        // PASO 1 — Confirmar que PostgreSQL comienza vacío.
        await using (DbConnection connection =
            await connectionFactory.OpenConnectionAsync(
                cancellationToken))
        {
            long initialTableCount =
                await connection.ExecuteScalarAsync<long>(
                    new CommandDefinition(
                        """
                        SELECT COUNT(*)
                        FROM information_schema.tables
                        WHERE table_schema = 'public'
                          AND table_type = 'BASE TABLE';
                        """,
                        cancellationToken: cancellationToken));

            Assert.Equal(0L, initialTableCount);
        }

        await using AsyncServiceScope migrationScope =
            services.CreateAsyncScope();

        BookingDbContext dbContext =
            migrationScope.ServiceProvider
                .GetRequiredService<BookingDbContext>();

        // PASO 2 — Descubrir todas las migrations del assembly.
        string[] expectedMigrations =
            dbContext.Database.GetMigrations().ToArray();

        Assert.NotEmpty(expectedMigrations);

        // Estos identificadores son puntos de referencia
        // conocidos del Módulo 22, no un límite que impida
        // agregar migraciones futuras.
        Assert.Contains(
            "20260723012919_InitialCreate",
            expectedMigrations);

        Assert.Contains(
            "20261022211314_AddPricingSeasonActivation",
            expectedMigrations);

        string[] pendingBefore =
            (await dbContext.Database.GetPendingMigrationsAsync(
                cancellationToken)).ToArray();

        // En una base nueva todas deben estar pendientes.
        Assert.Equal(expectedMigrations, pendingBefore);

        // CAMBIO 22.10:
        // Detectar modificaciones del modelo EF que todavía
        // no se hayan capturado en una migración.
        Assert.False(
            dbContext.Database.HasPendingModelChanges(),
            "The current EF model differs from the latest " +
            "migration model snapshot.");

        // PASO 3 — Ejecutar la secuencia real de migrations.
        await dbContext.Database.MigrateAsync(
            cancellationToken);

        string[] appliedMigrations =
            (await dbContext.Database.GetAppliedMigrationsAsync(
                cancellationToken)).ToArray();

        Assert.Equal(
            expectedMigrations,
            appliedMigrations);

        Assert.Empty(
            await dbContext.Database.GetPendingMigrationsAsync(
                cancellationToken));

        // PASO 4 — Verificar directamente el esquema PostgreSQL.
        await AssertExpectedSchemaAsync(
            connectionFactory,
            expectedMigrations.Length,
            cancellationToken);

        // PASO 5 — Crear datos mediante Application, no SQL
        // insertado manualmente.
        Guid propertyId = await CreatePropertyAsync(
            services,
            cancellationToken);

        // PASO 6 — Ejecutar MigrateAsync una segunda vez.
        // No debería volver a aplicar una migration ni
        // eliminar los datos que acabamos de crear.
        await dbContext.Database.MigrateAsync(
            cancellationToken);

        string[] appliedAgain =
            (await dbContext.Database.GetAppliedMigrationsAsync(
                cancellationToken)).ToArray();

        Assert.Equal(
            appliedMigrations,
            appliedAgain);

        Assert.Empty(
            await dbContext.Database.GetPendingMigrationsAsync(
                cancellationToken));

        // PASO 7 — Verificar que los datos sobreviven y
        // pueden leerse desde un scope independiente.
        await AssertPropertyReadableAsync(
            services,
            propertyId,
            cancellationToken);

        // PASO 8 — Confirmar que tampoco se modificó
        // el historial de migraciones en la segunda ejecución.
        await AssertMigrationHistoryCountAsync(
            connectionFactory,
            expectedMigrations.Length,
            cancellationToken);
    }

    private static async Task AssertExpectedSchemaAsync(
        IDbConnectionFactory connectionFactory,
        int expectedMigrationCount,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection =
            await connectionFactory.OpenConnectionAsync(
                cancellationToken);

        // TABLAS — Entidades principales, pagos y background jobs.
        string[] actualTables =
            (await connection.QueryAsync<string>(
                new CommandDefinition(
                    """
                    SELECT table_name
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_type = 'BASE TABLE';
                    """,
                    cancellationToken: cancellationToken)))
                .ToArray();

        string[] requiredTables =
        [
            "properties",
            "rentable_units",
            "rentable_unit_pricing",
            "rentable_unit_pricing_seasons",
            "bookings",
            "booking_price_snapshots",
            "payments",
            "payment_attempts",
            "idempotency_requests",
            "payment_webhook_events",
            "outbox_messages",
            "notification_deliveries",
            "__EFMigrationsHistory"
        ];

        foreach (string table in requiredTables)
        {
            Assert.Contains(table, actualTables);
        }

        // COLUMNAS — Comprobar cambios introducidos
        // por migraciones posteriores a InitialCreate.
        string[] actualColumns =
            (await connection.QueryAsync<string>(
                new CommandDefinition(
                    """
                    SELECT table_name || '.' || column_name
                    FROM information_schema.columns
                    WHERE table_schema = 'public';
                    """,
                    cancellationToken: cancellationToken)))
                .ToArray();

        string[] requiredColumns =
        [
            "properties.owner_subject_id",
            "bookings.booking_reference",
            "bookings.guest_access_token_hash",
            "bookings.approval_due_at_utc",
            "bookings.payment_due_at_utc",
            "payments.pending_initiation_key",
            "payments.pending_initiation_created_at_utc",
            "rentable_unit_pricing_seasons.is_active",
            "outbox_messages.attempt_count",
            "outbox_messages.next_attempt_at_utc"
        ];

        foreach (string column in requiredColumns)
        {
            Assert.Contains(column, actualColumns);
        }

        // ÍNDICES — Protecciones de unicidad y consultas
        // críticas para el funcionamiento de Bookify.
        string[] actualIndexes =
            (await connection.QueryAsync<string>(
                new CommandDefinition(
                    """
                    SELECT indexname
                    FROM pg_indexes
                    WHERE schemaname = 'public';
                    """,
                    cancellationToken: cancellationToken)))
                .ToArray();

        string[] requiredIndexes =
        [
            "ux_bookings_booking_reference",
            "ux_idempotency_requests_scope_key",
            "ux_payment_webhook_events_event_id",
            "ux_payment_attempts_payment_id_pending",
            "ix_bookings_pending_payment_due_at_utc",
            "ix_outbox_message_due"
        ];

        foreach (string index in requiredIndexes)
        {
            Assert.Contains(index, actualIndexes);
        }

        // RESTRICCIONES — Verificar que el esquema nuevo
        // conserva reglas importantes en PostgreSQL.
        string[] actualConstraints =
            (await connection.QueryAsync<string>(
                new CommandDefinition(
                    """
                    SELECT conname
                    FROM pg_constraint
                    WHERE connamespace = 'public'::regnamespace;
                    """,
                    cancellationToken: cancellationToken)))
                .ToArray();

        string[] requiredConstraints =
        [
            "ck_bookings_access_consistency",
            "ck_payments_initiation_reservation_consistency",
            "ck_payment_webhook_events_state",
            "ck_rentable_unit_pricing_seasons_date_range"
        ];

        foreach (string constraint in requiredConstraints)
        {
            Assert.Contains(constraint, actualConstraints);
        }

        // HISTORIAL — El número registrado en PostgreSQL
        // debe coincidir con el número de migraciones del assembly.
        long historyCount =
            await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    """
                    SELECT COUNT(*)
                    FROM "__EFMigrationsHistory";
                    """,
                    cancellationToken: cancellationToken));

        Assert.Equal(
            (long)expectedMigrationCount,
            historyCount);
    }

    private static async Task<Guid> CreatePropertyAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope =
            services.CreateAsyncScope();

        ICommandExecutor<CreatePropertyCommand, Guid> executor =
            scope.ServiceProvider.GetRequiredService<
                ICommandExecutor<CreatePropertyCommand, Guid>>();

        var command = new CreatePropertyCommand(
            "  Migration Smoke Property  ",
            "America/El_Salvador",
            new TimeOnly(15, 0),
            new TimeOnly(11, 0),
            $"migration-smoke-owner-{Guid.NewGuid():N}");

        Result<Guid> result = await executor.ExecuteAsync(
            command,
            cancellationToken);

        Assert.True(result.IsSuccess);

        Assert.NotEqual(
            Guid.Empty,
            result.Value);

        return result.Value;
    }

    private static async Task AssertPropertyReadableAsync(
        IServiceProvider services,
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope =
            services.CreateAsyncScope();

        IQueryExecutor<GetPropertyByIdQuery, PropertyDetailsReadModel>
            executor =
                scope.ServiceProvider.GetRequiredService<
                    IQueryExecutor<
                        GetPropertyByIdQuery,
                        PropertyDetailsReadModel>>();

        var query = new GetPropertyByIdQuery(propertyId);

        Result<PropertyDetailsReadModel> result =
            await executor.ExecuteAsync(
                query,
                cancellationToken);

        Assert.True(result.IsSuccess);

        Assert.Equal(
            propertyId,
            result.Value.Id);

        Assert.Equal(
            "Migration Smoke Property",
            result.Value.Name);

        Assert.Equal(
            "America/El_Salvador",
            result.Value.TimeZoneId);
    }

    private static async Task AssertMigrationHistoryCountAsync(
        IDbConnectionFactory connectionFactory,
        int expectedCount,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection =
            await connectionFactory.OpenConnectionAsync(
                cancellationToken);

        long actualCount =
            await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    """
                    SELECT COUNT(*)
                    FROM "__EFMigrationsHistory";
                    """,
                    cancellationToken: cancellationToken));

        Assert.Equal(
            (long)expectedCount,
            actualCount);
    }
}
