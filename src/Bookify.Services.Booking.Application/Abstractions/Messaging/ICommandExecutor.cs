using Bookify.Services.Booking.Domain.Shared;

namespace Bookify.Services.Booking.Application.Abstractions.Messaging;

public interface ICommandExecutor<in TCommand>
    where TCommand : ICommand
{
    Task<Result> ExecuteAsync(
        TCommand command,
        CancellationToken cancellationToken = default);
}

public interface ICommandExecutor<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> ExecuteAsync(
        TCommand command,
        CancellationToken cancellationToken = default);
}
