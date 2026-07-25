using Bookify.Services.Booking.Application.Abstractions.Messaging;

namespace Bookify.Services.Booking.Application.Properties.GetById;

public sealed record GetPropertyByIdQuery(Guid PropertyId)
    : IQuery<PropertyResponse>;
