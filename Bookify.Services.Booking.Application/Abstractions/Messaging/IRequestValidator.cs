using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Abstractions.Messaging;

public interface IRequestValidator<in TRequest>
{
    Result Validate(TRequest request);
}
