using Bookify.Services.Booking.Application.Abstractions.BackgroundJobs;
using Bookify.Services.Booking.Application.Abstractions.DomainEvents;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Availability.Get;
using Bookify.Services.Booking.Application.Availability.ReadModels;
using Bookify.Services.Booking.Application.BackgroundJobs.Completion;
using Bookify.Services.Booking.Application.BackgroundJobs.Expiration;
using Bookify.Services.Booking.Application.BackgroundJobs.Notifications;
using Bookify.Services.Booking.Application.Bookings.Administrative.Calendar;
using Bookify.Services.Booking.Application.Bookings.Administrative.GetById;
using Bookify.Services.Booking.Application.Bookings.Administrative.GetPaged;
using Bookify.Services.Booking.Application.Bookings.Administrative.ReadModels;
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
using Bookify.Services.Booking.Application.Payments.Administrative.GetByBooking;
using Bookify.Services.Booking.Application.Payments.Administrative.ReadModels;
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
using Bookify.Services.Booking.Application.RentableUnits.Management.Create;
using Bookify.Services.Booking.Application.RentableUnits.Management.GetAll;
using Bookify.Services.Booking.Application.RentableUnits.Management.GetById;
using Bookify.Services.Booking.Application.RentableUnits.Management.Pricing.Configure;
using Bookify.Services.Booking.Application.RentableUnits.Management.Pricing.Get;
using Bookify.Services.Booking.Application.RentableUnits.Management.Seasons;
using Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.Create;
using Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.GetAll;
using Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.GetById;
using Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.SetStatus;
using Bookify.Services.Booking.Application.RentableUnits.Management.Seasons.Update;
using Bookify.Services.Booking.Application.RentableUnits.Management.SetStatus;
using Bookify.Services.Booking.Application.RentableUnits.Management.Update;
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

        services.AddScoped<
            IRequestValidator<CreateRentableUnitCommand>,
            CreateRentableUnitCommandValidator>();

        services.AddScoped<
            ICommandHandler<CreateRentableUnitCommand, Guid>,
            CreateRentableUnitCommandHandler>();

        services.AddScoped<
            IRequestValidator<UpdateRentableUnitCommand>,
            UpdateRentableUnitCommandValidator>();

        services.AddScoped<
            ICommandHandler<UpdateRentableUnitCommand>,
            UpdateRentableUnitCommandHandler>();

        services.AddScoped<
            IRequestValidator<SetRentableUnitStatusCommand>,
            SetRentableUnitStatusCommandValidator>();

        services.AddScoped<
            ICommandHandler<SetRentableUnitStatusCommand>,
            SetRentableUnitStatusCommandHandler>();

        services.AddScoped<
            IRequestValidator<GetAdminRentableUnitsQuery>,
            GetAdminRentableUnitsQueryValidator>();

        services.AddScoped<
            IQueryHandler<GetAdminRentableUnitsQuery, IReadOnlyList<RentableUnitDetailsReadModel>>,
            GetAdminRentableUnitsQueryHandler>();

        services.AddScoped<
            IRequestValidator<GetAdminRentableUnitByIdQuery>,
            GetAdminRentableUnitByIdQueryValidator>();

        services.AddScoped<
            IQueryHandler<GetAdminRentableUnitByIdQuery, RentableUnitDetailsReadModel>,
            GetAdminRentableUnitByIdQueryHandler>();

        // ==========================================
        // Module: Administrative Pricing
        // ==========================================
        services.AddScoped<
            IRequestValidator<ConfigureRentableUnitPricingCommand>,
            ConfigureRentableUnitPricingCommandValidator>();

        services.AddScoped<
            ICommandHandler<ConfigureRentableUnitPricingCommand>,
            ConfigureRentableUnitPricingCommandHandler>();

        services.AddScoped<
            IRequestValidator<GetAdminRentableUnitPricingQuery>,
            GetAdminRentableUnitPricingQueryValidator>();

        services.AddScoped<
            IQueryHandler<GetAdminRentableUnitPricingQuery, AdminRentableUnitPricingReadModel>,
            GetAdminRentableUnitPricingQueryHandler>();

        // ==========================================
        // Module: Administrative Pricing Seasons
        // ==========================================
        services.AddScoped<
            IRequestValidator<CreatePricingSeasonCommand>,
            CreatePricingSeasonCommandValidator>();

        services.AddScoped<
            ICommandHandler<CreatePricingSeasonCommand, int>,
            CreatePricingSeasonCommandHandler>();

        services.AddScoped<
            IRequestValidator<UpdatePricingSeasonCommand>,
            UpdatePricingSeasonCommandValidator>();

        services.AddScoped<
            ICommandHandler<UpdatePricingSeasonCommand>,
            UpdatePricingSeasonCommandHandler>();

        services.AddScoped<
            IRequestValidator<SetPricingSeasonStatusCommand>,
            SetPricingSeasonStatusCommandValidator>();

        services.AddScoped<
            ICommandHandler<SetPricingSeasonStatusCommand>,
            SetPricingSeasonStatusCommandHandler>();

        services.AddScoped<
            IRequestValidator<GetPricingSeasonsQuery>,
            GetPricingSeasonsQueryValidator>();

        services.AddScoped<
            IQueryHandler<GetPricingSeasonsQuery, IReadOnlyList<PricingSeasonReadModel>>,
            GetPricingSeasonsQueryHandler>();

        services.AddScoped<
            IRequestValidator<GetPricingSeasonByIdQuery>,
            GetPricingSeasonByIdQueryValidator>();

        services.AddScoped<
            IQueryHandler<GetPricingSeasonByIdQuery, PricingSeasonReadModel>,
            GetPricingSeasonByIdQueryHandler>();

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
        // Module: Administrative Booking Reads
        // ==========================================
        services.AddScoped<
            IRequestValidator<GetAdminBookingsQuery>,
            GetAdminBookingsQueryValidator>();

        services.AddScoped<
            IQueryHandler<GetAdminBookingsQuery, PagedResult<AdminBookingListItemReadModel>>,
            GetAdminBookingsQueryHandler>();

        services.AddScoped<
            IRequestValidator<GetAdminBookingByIdQuery>,
            GetAdminBookingByIdQueryValidator>();

        services.AddScoped<
            IQueryHandler<GetAdminBookingByIdQuery, BookingDetailsReadModel>,
            GetAdminBookingByIdQueryHandler>();

        services.AddScoped<
            IRequestValidator<GetAdminBookingCalendarQuery>,
            GetAdminBookingCalendarQueryValidator>();

        services.AddScoped<
            IQueryHandler<GetAdminBookingCalendarQuery, IReadOnlyList<BookingCalendarItemReadModel>>,
            GetAdminBookingCalendarQueryHandler>();

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
        // Module: Administrative Payment Reads
        // ==========================================
        services.AddScoped<
            IRequestValidator<GetAdminBookingPaymentQuery>,
            GetAdminBookingPaymentQueryValidator>();

        services.AddScoped<
            IQueryHandler<GetAdminBookingPaymentQuery, AdminBookingPaymentReadModel>,
            GetAdminBookingPaymentQueryHandler>();

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
