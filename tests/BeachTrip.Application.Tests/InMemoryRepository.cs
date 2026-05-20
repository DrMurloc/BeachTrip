using BeachTrip.Application.Abstractions;
using BeachTrip.Domain.Abstractions;

namespace BeachTrip.Application.Tests;

internal sealed class InMemoryRepository<TAggregate, TId> : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    private readonly Dictionary<TId, TAggregate> _store = new();

    public Task<TAggregate?> Get(TId id, CancellationToken ct = default)
        => Task.FromResult<TAggregate?>(_store.TryGetValue(id, out var v) ? v : null);

    public Task Save(TAggregate aggregate, CancellationToken ct = default)
    {
        _store[aggregate.Id] = aggregate;
        aggregate.ClearUncommittedEvents();
        return Task.CompletedTask;
    }
}
