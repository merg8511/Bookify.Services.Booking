using Bookify.Services.Booking.Application;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Properties.Create;
using Bookify.Services.Booking.Application.Properties.GetById;
using Bookify.Services.Booking.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure();

var app = builder.Build();

app.MapGet("/health",
    () => Results.Ok(
        new
        {
            Status = "Healthy",
            Service = "Bookify.Services.Booking"
        }));

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

    app.MapPost(
        "/diagnostics/properties",
        async (
            CreatePropertyRequest request,
            ICommandHandler<
                CreatePropertyCommand,
                Guid> handler, CancellationToken cancelationToken) =>
        {
            var command = new CreatePropertyCommand(
                request.Name,
                request.TimeZoneId,
                request.CheckInTime,
                request.CheckOutTime);

            var result = await handler.HandleAsync(command, cancelationToken);

            if (result.IsFailure)
            {
                return Results.BadRequest(
                    new
                    {
                        result.Error.Code,
                        result.Error.Message
                    });
            }

            return Results.Ok(
                new
                {
                    PropertyId = result.Value
                });
        });

    app.MapGet("/diagnostics/properties/{propertyId:guid}",
        async (
            Guid propertyId,
            IQueryHandler<
                GetPropertyByIdQuery,
                PropertyResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetPropertyByIdQuery(propertyId);
            var result = await handler.HandleAsync(query, cancellationToken);

            if (result.IsFailure)
            {
                var errorResponse = new
                {
                    result.Error.Code,
                    result.Error.Message
                };

                if (result.Error.Code == "Property.NotFound")
                {
                    return Results.NotFound(errorResponse);
                }

                return Results.BadRequest(errorResponse);
            }

            return Results.Ok(result.Value);
        });
}

app.Run();

internal sealed record CreatePropertyRequest(
    string Name,
    string TimeZoneId,
    TimeOnly CheckInTime,
    TimeOnly CheckOutTime);
