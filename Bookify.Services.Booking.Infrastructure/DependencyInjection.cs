using Bookify.Services.Booking.Application.Abstractions.Time;
using Bookify.Services.Booking.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Bookify.Services.Booking.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services)
        {
            services.AddSingleton<IClock, SystemClock>();

            return services;
        }
    }
}
