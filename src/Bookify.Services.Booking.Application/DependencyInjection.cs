using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Abstractions.DomainEvents;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Availability.Get;
using Bookify.Services.Booking.Application.Availability.ReadModels;
using Bookify.Services.Booking.Application.BackgroundJobs.Completion;
using Bookify.Services.Booking.Application.BackgroundJobs.Expiration;
using Bookify.Services.Booking.Application.BackgroundJobs.Notifications;
using Bookify.Services.Booking.Application.Bookings.Approve;
using Bookify.Services.Booking.Application.Bookings.Cancel;
using Bookify.Services.Booking.Application.Bookings.Complete;
using Bookify.Services.Booking.Application.Bookings.Create;
using Bookify.Services.Booking.Application.Bookings.ExpireApproval;
using Bookify.Services.Booking.Application.Bookings.ExpirePayment;
using Bookify.Services.Booking.Application.Bookings.Get;
using Bookify.Services.Booking.Application.Bookings.ReadModels;
using Bookify.Services.Booking.Application.Bookings.Reject;
using Bookify.Services.Booking.Application.Common.Pagination;
using Bookify.Services.Booking.Application.DomainEvents;
using Bookify.Services.Booking.Application.Idempotency;
using Bookify.Services.Booking.Application.Messaging;
using Bookify.Services.Booking.Application.Notifications;
using Bookify.Services.Booking.Application.Payments.Cancellation;
using Bookify.Services.Booking.Application.Payments.GetStatus;
using Bookify.Services.Booking.Application.Payments.Initiate;
using Bookify.Services.Booking.Application.Payments.ReadModels;
using Bookify.Services.Booking.Application.Payments.Webhooks.Stripe;
using Bookify.Services.Booking.Application.Properties.Create;
using Bookify.Services.Booking.Application.Properties.GetById;
using Bookify.Services.Booking.Application.Properties.GetPaged;
using Bookify.Services.Booking.Application.Properties.ReadModels;
using Bookify.Services.Booking.Application.Properties.SetStatus;
using Bookify.Services.Booking.Application.Properties.Update;
using Bookify.Services.Booking.Application.RentableUnits.GetByProperty;
using Bookify.Services.Booking.Application.RentableUnits.ReadModels;
using Microsoft.Extensions.DependencyInjection;

namespace Bookify.Services.Booking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        // ==========================================
        // Core / CQRS Infrastructure
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

        services.AddScoped<
            IDomainEventDispatcher,
            DomainEventDispatcher>();

        // ==========================================
        // Module: Properties
        // ==========================================
        services.AddScoped<
            ICommandHandler<CreatePropertyCommand, Guid>,
            CreatePropertyCommandHandler>();

        services.AddScoped<
            IRequestValidator<UpdatePropertyCommand>,
            UpdatePropertyCommandValidator>();

        services.AddScoped<
            ICommandHandler<UpdatePropertyCommand>,
            UpdatePropertyCommandHandler>();

        services.AddScoped<
            IRequestValidator<SetPropertyStatusCommand>,
            SetPropertyStatusCommandValidator>();

        services.AddScoped<
            ICommandHandler<SetPropertyStatusCommand>,
            SetPropertyStatusCommandHandler>();

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
        // Module: Rentable Units
        // ==========================================
        services.AddScoped<
            IRequestValidator<GetPropertyUnitsQuery>,
            GetPropertyUnitsQueryValidator>();

        services.AddScoped<
            IQueryHandler<GetPropertyUnitsQuery, IReadOnlyList<RentableUnitListItemReadModel>>,
            GetPropertyUnitsQueryHandler>();

        // ==========================================
        // Module: Bookings
        // ==========================================
        services.AddScoped<
            IRequestValidator<CreateBookingCommand>,
            CreateBookingCommandValidator>();

        services.AddScoped<
            ICommandHandler<CreateBookingCommand, CreateBookingResult>,
            CreateBookingCommandHandler>();

        services.AddScoped<
            IRequestValidator<GetBookingQuery>,
            GetBookingQueryValidator>();

        services.AddScoped<
            IQueryHandler<GetBookingQuery, BookingDetailsReadModel>,
            GetBookingQueryHandler>();

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
            IRequestValidator<ExpireBookingApprovalCommand>,
            ExpireBookingApprovalCommandValidator>();

        services.AddScoped<
            ICommandHandler<ExpireBookingApprovalCommand>,
            ExpireBookingApprovalCommandHandler>();

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

        services.AddScoped<
            IRequestValidator<CancelBookingCommand>,
            CancelBookingCommandValidator>();

        services.AddScoped<
            ICommandHandler<CancelBookingCommand>,
            CancelBookingCommandHandler>();

        // ==========================================
        // Module: Availability
        // ==========================================
        services.AddScoped<
            IRequestValidator<GetAvailabilityQuery>,
            GetAvailabilityQueryValidator>();

        services.AddScoped<
            IQueryHandler<GetAvailabilityQuery, AvailabilityReadModel>,
            GetAvailabilityQueryHandler>();

        // ==========================================
        // Module: Idempotency
        // ==========================================
        services.AddScoped<
            IIdempotencyProcessor,
            IdempotencyProcessor>();

        // ==========================================
        // Module: Payments
        // ==========================================
        services.AddScoped<PaymentCancellationCoordinator>();

        services.AddScoped<
            IRequestValidator<InitiatePaymentCommand>,
            InitiatePaymentCommandValidator>();

        services.AddScoped<
            ICommandHandler<InitiatePaymentCommand, InitiatePaymentResponse>,
            InitiatePaymentCommandHandler>();

        services.AddScoped<
            IRequestValidator<GetPaymentStatusQuery>,
            GetPaymentStatusQueryValidator>();

        services.AddScoped<
            IQueryHandler<GetPaymentStatusQuery, PaymentStatusReadModel>,
            GetPaymentStatusQueryHandler>();

        services.AddScoped<
            IRequestValidator<ProcessStripeWebhookCommand>,
            ProcessStripeWebhookCommandValidator>();

        services.AddScoped<
            ICommandHandler<ProcessStripeWebhookCommand>,
            ProcessStripeWebhookCommandHandler>();

        // ==========================================
        // Module: Notifications
        // ==========================================
        services.AddScoped<BookingNotificationService>();

        // ==========================================
        // Module: Background Jobs
        // ==========================================
        services.AddKeyedScoped<
            IBackgroundJob,
            ApprovalExpirationJob>(BackgroundJobClaimKeys.ApprovalExpiration);

        services.AddKeyedScoped<
            IBackgroundJob,
            PaymentExpirationJob>(BackgroundJobClaimKeys.PaymentExpiration);

        services.AddKeyedScoped<
            IBackgroundJob,
            BookingCompletionJob>(BackgroundJobClaimKeys.BookingCompletion);

        services.AddKeyedScoped<
            IBackgroundJob,
            OutboxNotificationJob>(BackgroundJobClaimKeys.OutboxNotification);

        services.AddKeyedScoped<
            IBackgroundJob,
            PaymentDeadlineReminderJob>(BackgroundJobClaimKeys.PaymentDeadlineReminder);

        services.AddKeyedScoped<
            IBackgroundJob,
            UpcomingCheckInReminderJob>(BackgroundJobClaimKeys.UpcomingCheckInReminder);

        return services;
    }
}
