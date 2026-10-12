using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.Bookings.ExpireApproval;

public sealed record ExpireBookingApprovalCommand(Guid BookingId) : ICommand;
