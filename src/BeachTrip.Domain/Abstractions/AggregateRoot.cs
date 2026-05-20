namespace BeachTrip.Domain.Abstractions;

public abstract class AggregateRoot<TId> where TId : notnull
{
    private readonly List<DomainEvent> _uncommitted = new();

    public TId Id { get; protected set; } = default!;
    public long Version { get; private set; }
    public IReadOnlyList<DomainEvent> UncommittedEvents => _uncommitted;

    protected void Raise(DomainEvent @event)
    {
        Apply(@event);
        _uncommitted.Add(@event);
        Version++;
    }

    protected abstract void Apply(DomainEvent @event);

    public void ClearUncommittedEvents() => _uncommitted.Clear();

    public void Load(IEnumerable<DomainEvent> history)
    {
        foreach (var e in history)
        {
            Apply(e);
            Version++;
        }
    }
}
