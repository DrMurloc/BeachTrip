namespace BeachTrip.Infrastructure.EventStore;

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string aggregateType, string aggregateId, long expectedVersion)
        : base($"Concurrency conflict writing {aggregateType} {aggregateId} at version {expectedVersion}.")
    {
        AggregateType = aggregateType;
        AggregateId = aggregateId;
        ExpectedVersion = expectedVersion;
    }

    public string AggregateType { get; }
    public string AggregateId { get; }
    public long ExpectedVersion { get; }
}
