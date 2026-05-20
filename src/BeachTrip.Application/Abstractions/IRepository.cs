using BeachTrip.Domain.Abstractions;

namespace BeachTrip.Application.Abstractions;

public interface IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    Task<TAggregate?> Get(TId id, CancellationToken ct = default);
    Task Save(TAggregate aggregate, CancellationToken ct = default);
}
