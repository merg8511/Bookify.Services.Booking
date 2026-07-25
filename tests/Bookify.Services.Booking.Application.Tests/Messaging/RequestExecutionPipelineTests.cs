using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Messaging;
using Bookify.Services.Booking.Domain.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bookify.Services.Booking.Application.Tests.Messaging;

public sealed class RequestExecutionPipelineTests
{
    private static readonly Error InvalidRequestError =
        Error.Validation(
            "Test.InvalidRequest",
            "The test request is invalid");

    [Fact]
    public async Task ExecuteCommandAsync_WhenValidationSucceds_ShouldExecureHandler()
    {
        // ARRANGE
        var handler = new TestCommandHandler();

        IRequestValidator<TestCommand>[] validators =
            [
                new TestCommandValidator()
            ];

        var executor =
            new CommandExecutor<
                TestCommand,
                Guid>(
                handler,
                validators,
                NullLogger<
                    CommandExecutor<
                        TestCommand,
                        Guid>>.Instance);

        var command = new TestCommand(IsValid: true);

        // ACT
        Result<Guid> result = await executor.ExecuteAsync(command);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.True(handler.WasCalled);
        Assert.Equal(handler.ReturnedId, result.Value);
    }

    [Fact]
    public async Task ExecuteCommandAsync_WhenValidationFails_ShouldNotExecuteHandler()
    {
        // ARRANGE
        var handler =
            new TestCommandHandler();

        IRequestValidator<TestCommand>[] validators =
        [
            new TestCommandValidator()
        ];

        var executor =
            new CommandExecutor<
                TestCommand,
                Guid>(
                handler,
                validators,
                NullLogger<
                    CommandExecutor<
                        TestCommand,
                        Guid>>.Instance);

        var command =
            new TestCommand(
                IsValid: false);

        // ACT
        Result<Guid> result =
            await executor.ExecuteAsync(
                command);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            InvalidRequestError,
            result.Error);

        Assert.False(
            handler.WasCalled);
    }

    [Fact]
    public async Task ExecuteQueryAsync_WhenValidationSucceeds_ShouldExecuteHandler()
    {
        // ARRANGE
        var handler =
            new TestQueryHandler();

        IRequestValidator<TestQuery>[] validators =
        [
            new TestQueryValidator()
        ];

        var executor =
            new QueryExecutor<
                TestQuery,
                string>(
                handler,
                validators,
                NullLogger<
                    QueryExecutor<
                        TestQuery,
                        string>>.Instance);

        var query =
            new TestQuery(
                IsValid: true);

        // ACT
        Result<string> result =
            await executor.ExecuteAsync(
                query);

        // ASSERT
        Assert.True(result.IsSuccess);
        Assert.Equal("Response", result.Value);
        Assert.True(handler.WasCalled);
    }

    [Fact]
    public async Task ExecuteQueryAsync_WhenValidationFails_ShouldNotExecuteHandler()
    {
        // ARRANGE
        var handler =
            new TestQueryHandler();

        IRequestValidator<TestQuery>[] validators =
        [
            new TestQueryValidator()
        ];

        var executor =
            new QueryExecutor<
                TestQuery,
                string>(
                handler,
                validators,
                NullLogger<
                    QueryExecutor<
                        TestQuery,
                        string>>.Instance);

        var query =
            new TestQuery(
                IsValid: false);

        // ACT
        Result<string> result =
            await executor.ExecuteAsync(
                query);

        // ASSERT
        Assert.True(result.IsFailure);

        Assert.Equal(
            InvalidRequestError,
            result.Error);

        Assert.False(
            handler.WasCalled);
    }

    private sealed record TestCommand(
        bool IsValid)
        : ICommand<Guid>;

    private sealed class TestCommandHandler
        : ICommandHandler<TestCommand, Guid>
    {
        public Guid ReturnedId { get; } = Guid.NewGuid();

        public bool WasCalled { get; private set; }

        public Task<Result<Guid>> HandleAsync(
            TestCommand command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WasCalled = true;

            return Task.FromResult(
                Result<Guid>.Success(ReturnedId));
        }
    }

    private sealed class TestCommandValidator
        : IRequestValidator<TestCommand>
    {
        public Result Validate(TestCommand request)
        {
            return request.IsValid
                ? Result.Success()
                : Result.Failure(
                    InvalidRequestError);
        }
    }

    private sealed record TestQuery(
        bool IsValid)
        : IQuery<string>;

    private sealed class TestQueryHandler
        : IQueryHandler<TestQuery, string>
    {
        public bool WasCalled { get; private set; }

        public Task<Result<string>> HandleAsync(
            TestQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WasCalled = true;

            return Task.FromResult(
                Result<string>.Success("Response"));
        }
    }

    private sealed class TestQueryValidator
        : IRequestValidator<TestQuery>
    {
        public Result Validate(TestQuery request)
        {
            return request.IsValid
                ? Result.Success()
                : Result.Failure(InvalidRequestError);
        }
    }
}
