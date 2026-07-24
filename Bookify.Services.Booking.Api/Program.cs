using Bookify.Services.Booking.Api.Endpoints;
using Bookify.Services.Booking.Application;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Infrastructure;
using Bookify.Services.Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

string? configuredConnectionString = builder.Configuration.GetConnectionString("Database");

if (string.IsNullOrWhiteSpace(configuredConnectionString))
{
    throw new InvalidOperationException(
        "The database connection string " +
        "'ConnectionStrings:Database' is missing");
}

string connectionString = configuredConnectionString;

builder.Services
    .AddApplication()
    .AddInfrastructure(connectionString);

var app = builder.Build();

app.MapGet("/health",
    () => Results.Ok(
        new
        {
            Status = "Healthy",
            Service = "Bookify.Services.Booking"
        }));

app.MapApiEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapGet(
        "/diagnostics/time",
        (IClock clock) =>
            Results.Ok(
                new
                {
                    UtcNow = clock.UtcNow
                }));

    app.MapGet(
        "/diagnostics/database",
        async (
            BookingDbContext dbContext,
            CancellationToken cancelationToken) =>
        {
            bool canConnect = await dbContext.Database.CanConnectAsync(cancelationToken);

            if (!canConnect)
            {
                return Results.Problem(
                    title: "Database unavailable",
                    detail: "The application could not connect to PostgreSQL",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(
                new
                {
                    Status = "Connected",
                    Provider = dbContext.Database.ProviderName
                });
        });

    app.MapGet(
    "/diagnostics/database/model",
    (BookingDbContext dbContext) =>
    {
        var entities =
            dbContext.Model
                .GetEntityTypes()
                .Select(
                    entityType =>
                        new
                        {
                            Entity =
                                entityType.ClrType.Name,

                            Table =
                                entityType.GetTableName()
                        })
                .OrderBy(
                    entity =>
                        entity.Entity)
                .ToArray();

        return Results.Ok(entities);
    });
}

app.Run();
