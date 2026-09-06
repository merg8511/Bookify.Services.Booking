using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.Payments.Webhooks.Stripe;

public sealed record ProcessStripeWebhookCommand(string RawBody) : ICommand;
