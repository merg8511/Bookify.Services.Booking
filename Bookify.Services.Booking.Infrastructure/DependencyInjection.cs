using Bookify.Services.Booking.Application.Abstractions.Persistence;
using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Application.Properties.GetById;
using Bookify.Services.Booking.Infrastructure.Persistence.InMemory;
using Bookify.Services.Booking.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Bookify.Services.Booking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();

        services.AddSingleton<InMemoryBookingStore>();

        services.AddScoped<InMemoryUnitOfWork>();

        services.AddScoped<IUnitOfWork>(
            serviceProvider =>
                serviceProvider.GetRequiredService<InMemoryUnitOfWork>());

        services.AddScoped<
            IPropertyRepository,
            InMemoryPropertyRepository>();

        services.AddScoped<
            IPropertyReadService,
            InMemoryPropertyReadService>();

        return services;
    }
}
