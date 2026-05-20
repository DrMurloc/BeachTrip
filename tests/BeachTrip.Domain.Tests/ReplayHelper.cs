using System.Reflection;
using BeachTrip.Domain.Abstractions;

namespace BeachTrip.Domain.Tests;

internal static class ReplayHelper
{
    public static TAggregate Replay<TAggregate, TId>(IEnumerable<DomainEvent> history)
        where TAggregate : AggregateRoot<TId>
        where TId : notnull
    {
        var ctor = typeof(TAggregate).GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null)
            ?? throw new InvalidOperationException($"{typeof(TAggregate).Name} needs a parameterless constructor.");

        var aggregate = (TAggregate)ctor.Invoke(null);
        aggregate.Load(history);
        return aggregate;
    }
}
