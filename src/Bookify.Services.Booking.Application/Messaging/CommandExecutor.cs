using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Bookify.Services.Booking.Application.Messaging;

public sealed class CommandExecutor<TCommand>
    : ICommandExecutor<TCommand>
    where TCommand : ICommand
{
    private readonly ICommandHandler<TCommand> _handler;
    private readonly IEnumerable<
        IRequestValidator<TCommand>> _validators;
    private readonly ILogger<
        CommandExecutor<TCommand>> _logger;

    public CommandExecutor(
        ICommandHandler<TCommand> handler,
        IEnumerable<IRequestValidator<TCommand>> validators,
        ILogger<CommandExecutor<TCommand>> logger)
    {
        _handler = handler;
        _validators = validators.ToArray();
        _logger = logger;
    }

    public async Task<Result> ExecuteAsync(
        TCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        cancellationToken.ThrowIfCancellationRequested();

        string commandName = typeof(TCommand).Name;

        long startedAt =
            Stopwatch.GetTimestamp();

        _logger.LogInformation(
            "Executing command {CommandName}",
            commandName);

        Result validationResult = RequestValidation.Validate(
            command,
            _validators);

        if (validationResult.IsFailure)
        {
            LogCompleted(
                commandName,
                validationResult,
                startedAt);

            return validationResult;
        }

        Result result = await _handler.HandleAsync(
            command,
            cancellationToken);

        LogCompleted(
            commandName,
            result,
            startedAt);

        return result;
    }

    private void LogCompleted(
        string commandName,
        Result result,
        long startedAt)
    {
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startedAt);

        string? errorCode =
            result.IsFailure
                ? result.Error.Code
                : null;

        _logger.LogInformation(
            "Completed command {CommandName}. " +
            "Success: {IsSuccess}. " +
            "ErrorCode: {ErrorCode}. " +
            "DurationMs: {DurationMs}",
            commandName,
            result.IsSuccess,
            errorCode,
            elapsed.TotalMilliseconds);

    }
}

public sealed class CommandExecutor<TCommand, TResponse>
    : ICommandExecutor<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private readonly ICommandHandler<
        TCommand,
        TResponse> _handler;

    private readonly IEnumerable<
        IRequestValidator<TCommand>> _validators;

    private readonly ILogger<CommandExecutor<TCommand, TResponse>> _logger;

    public CommandExecutor(
        ICommandHandler<TCommand, TResponse> handler,
        IEnumerable<IRequestValidator<TCommand>> validators,
        ILogger<CommandExecutor<TCommand, TResponse>> logger)
    {
        _handler = handler;
        _validators = validators;
        _logger = logger;
    }

    public async Task<Result<TResponse>> ExecuteAsync(
        TCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        string commandName = typeof(TCommand).Name;

        long startedAt = Stopwatch.GetTimestamp();

        _logger.LogInformation(
            "Executing command {CommandName}",
            commandName);

        Result validationResult =
            RequestValidation.Validate(
                command,
                _validators);

        if (validationResult.IsFailure)
        {
            Result<TResponse> failureResult = Result<TResponse>
                .Failure(
                validationResult.Error);

            LogCompleted(
                commandName,
                failureResult,
                startedAt);

            return failureResult;
        }

        Result<TResponse> result = await _handler.HandleAsync(
            command,
            cancellationToken);

        LogCompleted(
            commandName,
            result,
            startedAt);

        return result;
    }

    private void LogCompleted(
        string commandName,
        Result<TResponse> result,
        long startedAt)
    {
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startedAt);

        string? errorCode =
            result.IsFailure
            ? result.Error.Code
            : null;

        _logger.LogInformation(
            "Completed command {CommandName}. " +
            "Success: {IsSuccess}. " +
            "ErrorCode: {ErrorCode}" +
            "DurationMs: {DurationMs}",
            commandName,
            result.IsSuccess,
            errorCode,
            elapsed.TotalMilliseconds);
    }
}
