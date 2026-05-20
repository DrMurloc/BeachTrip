using System.Text.Json;

namespace BeachTrip.Infrastructure.EventStore;

// Shape of one event document in the events container.
// Partition key path = /aggregateId so the whole stream for an aggregate lives in one partition.
public sealed class CosmosEventDocument
{
    public string Id { get; set; } = "";
    public string AggregateType { get; set; } = "";
    public string AggregateId { get; set; } = "";
    public string EventType { get; set; } = "";
    public long Version { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public JsonElement Data { get; set; }
}
