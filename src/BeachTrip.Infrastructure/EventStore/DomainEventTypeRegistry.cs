using System.Reflection;
using BeachTrip.Domain.Abstractions;
using BeachTrip.Domain.Attendees;

namespace BeachTrip.Infrastructure.EventStore;

public sealed class DomainEventTypeRegistry
{
    private readonly IReadOnlyDictionary<string, Type> _byName;

    public DomainEventTypeRegistry(IEnumerable<Type> eventTypes)
    {
        var dict = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var t in eventTypes)
            dict[t.Name] = t;
        _byName = dict;
    }

    public Type Resolve(string name) =>
        _byName.TryGetValue(name, out var t)
            ? t
            : throw new InvalidOperationException($"Unknown domain event type '{name}'.");

    public static DomainEventTypeRegistry FromDomainAssembly()
    {
        var assembly = typeof(Attendee).Assembly;
        var types = assembly.GetTypes()
            .Where(t => !t.IsAbstract
                        && typeof(DomainEvent).IsAssignableFrom(t)
                        && t != typeof(DomainEvent));
        return new DomainEventTypeRegistry(types);
    }
}
