using Bookify.Services.Booking.Domain.Shared.DomainEvents;

namespace Bookify.Services.Booking.Domain.Tests.Shared.DomainEvents;

public sealed class AggregateRootTests
{
    [Fact]
    public void RaiseDomainEvent_ShouldStoreDomainEvent()
    {
        // ARRANGE
        var aggregateRoot = new TestAggregateRoot();
        var domainEvent = new TestDomainEvent("first");

        // ACT
        aggregateRoot.Raise(domainEvent);

        // ASSERT
        IReadOnlyCollection<IDomainEvent> domainEvents =
            aggregateRoot.GetDomainEvents();

        IDomainEvent storedEvent =
            Assert.Single(domainEvents);

        Assert.Same(
            domainEvent,
            storedEvent);
    }

    [Fact]
    public void RaiseDomainEvent_WithMultipleEvents_ShouldPreserveOrder()
    {
        // ARRANGE
        var aggregateRoot = new TestAggregateRoot();

        var firstEvent =
            new TestDomainEvent("first");

        var secondEvent =
            new TestDomainEvent("second");

        // ACT
        aggregateRoot.Raise(firstEvent);
        aggregateRoot.Raise(secondEvent);

        // ASSERT
        Assert.Collection(
            aggregateRoot.GetDomainEvents(),
            domainEvent =>
                Assert.Same(
                    firstEvent,
                    domainEvent),
            domainEvent =>
                Assert.Same(
                    secondEvent,
                    domainEvent));
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllDomainEvents()
    {
        // ARRANGE
        var aggregateRoot = new TestAggregateRoot();

        aggregateRoot.Raise(
            new TestDomainEvent("first"));

        aggregateRoot.Raise(
            new TestDomainEvent("second"));

        // ACT
        aggregateRoot.ClearDomainEvents();

        // ASSERT
        Assert.Empty(
            aggregateRoot.GetDomainEvents());
    }

    [Fact]
    public void RaiseDomainEvent_WithNullEvent_ShouldThrow()
    {
        // ARRANGE
        var aggregateRoot =
            new TestAggregateRoot();

        // ACT
        void Action()
        {
            aggregateRoot.Raise(null!);
        }

        // ASSERT
        Assert.Throws<ArgumentNullException>(
            Action);
    }

    private sealed class TestAggregateRoot
        : AggregateRoot
    {
        public void Raise(
            IDomainEvent domainEvent)
        {
            RaiseDomainEvent(domainEvent);
        }
    }

    private sealed record TestDomainEvent(
        string Name)
        : IDomainEvent;
}
