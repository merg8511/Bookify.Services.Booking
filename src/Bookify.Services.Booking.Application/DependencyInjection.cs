using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Availability.Get;
using Bookify.Services.Booking.Application.Availability.ReadModels;
using Bookify.Services.Booking.Application.Bookings.Approve;
using Bookify.Services.Booking.Application.Bookings.Complete;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Application.Bookings.ExpirePayment;
using Bookify.Services.Booking.Application.Bookings.MarkAsPaid;
using Bookify.Services.Booking.Application.Bookings.Reject;
using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Application.Idempotency;
using Bookify.Services.Booking.Application.Messaging;
using Bookify.Services.Booking.Application.Properties.Create;
using Bookify.Services.Booking.Application.Properties.GetById;
using Bookify.Services.Booking.Application.Properties.GetPaged;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        // ==========================================
        //  Core / CQRS Infrastructure
        // ==========================================
        services.AddScoped(
            typeof(ICommandExecutor<>),
            typeof(CommandExecutor<>));

        services.AddScoped(
            typeof(ICommandExecutor<,>),
            typeof(CommandExecutor<,>));

        services.AddScoped(
            typeof(IQueryExecutor<,>),
            typeof(QueryExecutor<,>));


        // ==========================================
        //  Module: Properties
        // ==========================================
        services.AddScoped<
            ICommandHandler<CreatePropertyCommand, Guid>,
            CreatePropertyCommandHandler>();

        services.AddScoped<
            IRequestValidator<GetPropertyByIdQuery>,
            GetPropertyByIdQueryValidator>();

        services.AddScoped<
            IQueryHandler<GetPropertyByIdQuery, PropertyDetailsReadModel>,
            GetPropertyByIdQueryHandler>();

        services.AddScoped<
            IRequestValidator<GetPropertiesQuery>,
            GetPropertiesQueryValidator>();

        services.AddScoped<
            IQueryHandler<GetPropertiesQuery, PagedResult<PropertyListItemReadModel>>,
            GetPropertiesQueryHandler>();


        // ==========================================
        //  Module: Bookings
        // ==========================================
        services.AddScoped<
            IRequestValidator<CreateBookingCommand>,
            CreateBookingCommandValidator>();

        services.AddScoped<
            ICommandHandler<CreateBookingCommand, CreateBookingResult>,
            CreateBookingCommandHandler>();

        services.AddScoped<
            IRequestValidator<ApproveBookingCommand>,
            ApproveBookingCommandValidator>();

        services.AddScoped<
            ICommandHandler<ApproveBookingCommand>,
            ApproveBookingCommandHandler>();

        services.AddScoped<
            IRequestValidator<RejectBookingCommand>,
            RejectBookingCommandValidator>();

        services.AddScoped<
            ICommandHandler<RejectBookingCommand>,
            RejectBookingCommandHandler>();

        services.AddScoped<
            IRequestValidator<MarkBookingAsPaidCommand>,
            MarkBookingAsPaidCommandValidator>();

        services.AddScoped<
            ICommandHandler<MarkBookingAsPaidCommand>,
            MarkBookingAsPaidCommandHandler>();

        services.AddScoped<
            IRequestValidator<ExpireBookingPaymentCommand>,
            ExpireBookingPaymentCommandValidator>();

        services.AddScoped<
            ICommandHandler<ExpireBookingPaymentCommand>,
            ExpireBookingPaymentCommandHandler>();

        services.AddScoped<
            IRequestValidator<CompleteBookingCommand>,
            CompleteBookingCommandValidator>();

        services.AddScoped<
            ICommandHandler<CompleteBookingCommand>,
            CompleteBookingCommandHandler>();


        // ==========================================
        //  Module: Availability
        // ==========================================
        services.AddScoped<
            IRequestValidator<GetAvailabilityQuery>,
            GetAvailabilityQueryValidator>();

        services.AddScoped<
            IQueryHandler<GetAvailabilityQuery, AvailabilityReadModel>,
            GetAvailabilityQueryHandler>();


        // ==========================================
        //  Module: Idempotency
        // ==========================================
        services.AddScoped<
            IIdempotencyProcessor,
            IdempotencyProcessor>();

        return services;
    }
}
