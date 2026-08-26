using Application.Events;
using Domain.Events;

namespace Application.UnitTests.Fakes;

public class FakeDomainEventDispatcher : IDomainEventDispatcher
{
    public readonly List<IDomainEvent> DispatchedEvents = new();

    public Task DispatchAsync(IEnumerable<IDomainEvent> events)
    {
        DispatchedEvents.AddRange(events);
        return Task.CompletedTask;
    }
}
