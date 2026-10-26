using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.Properties.SetStatus;

public sealed record SetPropertyStatusCommand(Guid PropertyId, bool IsActive) : ICommand;
