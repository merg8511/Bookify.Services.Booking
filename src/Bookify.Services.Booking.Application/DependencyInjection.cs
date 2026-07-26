using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Messaging;
using Bookify.Services.Booking.Application.Properties.Create;
using Bookify.Services.Booking.Application.Properties.GetById;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped(
            typeof(ICommandExecutor<>),
            typeof(CommandExecutor<>));

        services.AddScoped(
            typeof(ICommandExecutor<,>),
            typeof(CommandExecutor<,>));

        services.AddScoped(
            typeof(IQueryExecutor<,>),
            typeof(QueryExecutor<,>));

        services.AddScoped<
            ICommandHandler<CreatePropertyCommand, Guid>,
            CreatePropertyCommandHandler>();

        services.AddScoped<
            IQueryHandler<
                GetPropertyByIdQuery,
                PropertyDetailsReadModel>,
            GetPropertyByIdQueryHandler>();

        services.AddScoped<
            IRequestValidator<GetPropertyByIdQuery>,
            GetPropertyByIdQueryValidator>();

        return services;
    }
}
