using System.Net;
using System.Reflection;
using System.Text.Json;
using BeachTrip.Application.Abstractions;
using BeachTrip.Domain.Abstractions;
using Microsoft.Azure.Cosmos;

namespace BeachTrip.Infrastructure.EventStore;

public sealed class CosmosEventRepository<TAggregate, TId> : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    private static readonly string AggregateTypeName = typeof(TAggregate).Name;

    private readonly Container _container;
    private readonly DomainEventTypeRegistry _registry;
    private readonly JsonSerializerOptions _json;

    public CosmosEventRepository(
        Container container,
        DomainEventTypeRegistry registry,
        JsonSerializerOptions json)
    {
        _container = container;
        _registry = registry;
        _json = json;
    }

    public async Task<TAggregate?> Get(TId id, CancellationToken ct = default)
    {
        var partitionKey = new PartitionKey(id.ToString());
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.aggregateType = @t ORDER BY c.version")
            .WithParameter("@t", AggregateTypeName);

        var iterator = _container.GetItemQueryIterator<CosmosEventDocument>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = partitionKey });

        var events = new List<DomainEvent>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(ct);
            foreach (var doc in page)
            {
                var clrType = _registry.Resolve(doc.EventType);
                var domainEvent = (DomainEvent)doc.Data.Deserialize(clrType, _json)!;
                events.Add(domainEvent);
            }
        }

        if (events.Count == 0) return null;

        var aggregate = CreateBlank();
        aggregate.Load(events);
        return aggregate;
    }

    public async Task Save(TAggregate aggregate, CancellationToken ct = default)
    {
        var uncommitted = aggregate.UncommittedEvents.ToArray();
        if (uncommitted.Length == 0) return;

        var startVersion = aggregate.Version - uncommitted.Length;
        var partitionKey = new PartitionKey(aggregate.Id.ToString());

        var version = startVersion;
        foreach (var domainEvent in uncommitted)
        {
            version++;
            var data = JsonSerializer.SerializeToElement(domainEvent, domainEvent.GetType(), _json);
            var doc = new CosmosEventDocument
            {
                Id = $"{AggregateTypeName}|{aggregate.Id}|{version}",
                AggregateType = AggregateTypeName,
                AggregateId = aggregate.Id.ToString()!,
                EventType = domainEvent.GetType().Name,
                Version = version,
                OccurredAt = domainEvent.OccurredAt,
                Data = data,
            };

            try
            {
                await _container.CreateItemAsync(doc, partitionKey, cancellationToken: ct);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                throw new ConcurrencyConflictException(AggregateTypeName, aggregate.Id.ToString()!, version);
            }
        }
        aggregate.ClearUncommittedEvents();
    }

    private static TAggregate CreateBlank()
    {
        var ctor = typeof(TAggregate).GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null)
            ?? throw new InvalidOperationException($"{typeof(TAggregate).Name} needs a parameterless constructor.");
        return (TAggregate)ctor.Invoke(null);
    }
}
