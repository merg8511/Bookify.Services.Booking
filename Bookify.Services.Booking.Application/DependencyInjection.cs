using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Properties.Create;
using Bookify.Services.Booking.Application.Properties.GetById;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Bookify.Services.Booking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<
            ICommandHandler<CreatePropertyCommand, Guid>,
            CreatePropertyCommandHandler>();

        services.AddScoped<
            IQueryHandler<
                GetPropertyByIdQuery,
                PropertyResponse>,
            GetPropertyByIdQueryHandler>();

        return services;
    }
}
