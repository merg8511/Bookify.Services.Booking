using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Properties.ReadModels;

namespace Bookify.Services.Booking.Application.Properties.GetById;

public sealed record GetPropertyByIdQuery(Guid PropertyId)
    : IQuery<PropertyDetailsReadModel>;
