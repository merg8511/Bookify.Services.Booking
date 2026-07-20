using Bookify.Services.Booking.Application;
using Bookify.Services.Booking.Application.Abstractions.Time;
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
}

app.Run();
