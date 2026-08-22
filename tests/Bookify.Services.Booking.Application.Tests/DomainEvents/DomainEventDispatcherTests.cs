using Bookify.Services.Booking.Application.Abstractions.DomainEvents;
using Bookify.Services.Booking.Application.DomainEvents;
using Bookify.Services.Booking.Domain.Bookings.Events;
using Bookify.Services.Booking.Domain.Shared.DomainEvents;

namespace Bookify.Services.Booking.Application.Tests.DomainEvents;

public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_WithSingleHandler_ShouldInvokeHandler()
    {
        // ARRANGE
        var serviceProvider =
            new TestServiceProvider();

        var handler =
            new RecordingHandler(
                "first",
                []);

        serviceProvider.AddHandlers<
            BookingCreatedDomainEvent>(
                handler);

        var dispatcher =
            new DomainEventDispatcher(
                serviceProvider);

        var domainEvent =
            new BookingCreatedDomainEvent(
                Guid.NewGuid());

        // ACT
        await dispatcher.DispatchAsync(
            [domainEvent]);

        // ASSERT
        Assert.Equal(
            1,
            handler.InvocationCount);

        Assert.Same(
            domainEvent,
            handler.LastDomainEvent);
    }

    [Fact]
    public async Task DispatchAsync_WithMultipleHandlers_ShouldInvokeAllInOrder()
    {
        // ARRANGE
        var invocations =
            new List<string>();

        var firstHandler =
            new RecordingHandler(
                "first",
                invocations);

        var secondHandler =
            new RecordingHandler(
                "second",
                invocations);

        var serviceProvider =
            new TestServiceProvider();

        serviceProvider.AddHandlers<
            BookingCreatedDomainEvent>(
                firstHandler,
                secondHandler);

        var dispatcher =
            new DomainEventDispatcher(
                serviceProvider);

        var domainEvent =
            new BookingCreatedDomainEvent(
                Guid.NewGuid());

        // ACT
        await dispatcher.DispatchAsync(
            [domainEvent]);

        // ASSERT
        Assert.Equal(
            ["first", "second"],
            invocations);
    }

    [Fact]
    public async Task DispatchAsync_WithoutHandlers_ShouldCompleteSuccessfully()
    {
        // ARRANGE
        var serviceProvider =
            new TestServiceProvider();

        var dispatcher =
            new DomainEventDispatcher(
                serviceProvider);

        var domainEvent =
            new BookingCreatedDomainEvent(
                Guid.NewGuid());

        // ACT
        Task Action()
        {
            return dispatcher.DispatchAsync(
                [domainEvent]);
        }

        // ASSERT
        await Action();
    }

    [Fact]
    public async Task DispatchAsync_WhenHandlerThrows_ShouldPropagateExceptionAndStopDispatching()
    {
        // ARRANGE
        var invocations =
            new List<string>();

        var throwingHandler =
            new ThrowingHandler(
                invocations);

        var subsequentHandler =
            new RecordingHandler(
                "subsequent",
                invocations);

        var serviceProvider =
            new TestServiceProvider();

        serviceProvider.AddHandlers<
            BookingCreatedDomainEvent>(
                throwingHandler,
                subsequentHandler);

        var dispatcher =
            new DomainEventDispatcher(
                serviceProvider);

        var domainEvent =
            new BookingCreatedDomainEvent(
                Guid.NewGuid());

        // ACT
        Task Action()
        {
            return dispatcher.DispatchAsync(
                [domainEvent]);
        }

        // ASSERT
        InvalidOperationException exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    Action);

        Assert.Equal(
            "Domain event handler failed.",
            exception.Message);

        Assert.Equal(
            ["throwing"],
            invocations);

        Assert.Equal(
            0,
            subsequentHandler.InvocationCount);
    }

    private sealed class RecordingHandler
        : IDomainEventHandler<
            BookingCreatedDomainEvent>
    {
        private readonly string _name;
        private readonly List<string> _invocations;

        public RecordingHandler(
            string name,
            List<string> invocations)
        {
            _name = name;
            _invocations = invocations;
        }

        public int InvocationCount { get; private set; }

        public BookingCreatedDomainEvent?
            LastDomainEvent
        { get; private set; }

        public Task HandleAsync(
            BookingCreatedDomainEvent domainEvent,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                domainEvent);

            cancellationToken
                .ThrowIfCancellationRequested();

            InvocationCount++;
            LastDomainEvent = domainEvent;

            _invocations.Add(
                _name);

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler
        : IDomainEventHandler<
            BookingCreatedDomainEvent>
    {
        private readonly List<string> _invocations;

        public ThrowingHandler(
            List<string> invocations)
        {
            _invocations = invocations;
        }

        public Task HandleAsync(
            BookingCreatedDomainEvent domainEvent,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                domainEvent);

            cancellationToken
                .ThrowIfCancellationRequested();

            _invocations.Add(
                "throwing");

            throw new InvalidOperationException(
                "Domain event handler failed.");
        }
    }

    private sealed class TestServiceProvider
        : IServiceProvider
    {
        private readonly Dictionary<Type, object>
            _services = [];

        public void AddHandlers<TDomainEvent>(
            params IDomainEventHandler<
                TDomainEvent>[] handlers)
            where TDomainEvent : IDomainEvent
        {
            _services[
                typeof(
                    IEnumerable<
                        IDomainEventHandler<
                            TDomainEvent>>)] =
                handlers;
        }

        public object? GetService(
            Type serviceType)
        {
            ArgumentNullException.ThrowIfNull(
                serviceType);

            if (_services.TryGetValue(
                    serviceType,
                    out object? service))
            {
                return service;
            }

            if (serviceType.IsGenericType &&
                serviceType.GetGenericTypeDefinition() ==
                typeof(IEnumerable<>))
            {
                Type elementType =
                    serviceType
                        .GetGenericArguments()[0];

                return Array.CreateInstance(
                    elementType,
                    0);
            }

            return null;
        }
    }
}
