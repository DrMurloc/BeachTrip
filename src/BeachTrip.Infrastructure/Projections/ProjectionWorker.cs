using BeachTrip.Application.Abstractions;
using BeachTrip.Domain;
using BeachTrip.Domain.Attendees;
using BeachTrip.Domain.Carpools;
using BeachTrip.Domain.Parking;
using BeachTrip.Domain.Rooms;
using BeachTrip.Infrastructure.EventStore;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BeachTrip.Infrastructure.Projections;

// Subscribes to the events container's change feed. For each event, re-reads the aggregate
// and upserts the corresponding view document. The aggregate-rebuild approach trades a
// small Cosmos read per event for not having to write per-event mutation logic.
public sealed class ProjectionWorker : BackgroundService
{
    private readonly CosmosClient _cosmos;
    private readonly IServiceProvider _services;
    private readonly ILogger<ProjectionWorker> _log;
    private ChangeFeedProcessor? _processor;

    public ProjectionWorker(CosmosClient cosmos, IServiceProvider services, ILogger<ProjectionWorker> log)
    {
        _cosmos = cosmos;
        _services = services;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var events = _cosmos.GetContainer(BeachTripCosmosOptions.DatabaseName, BeachTripCosmosOptions.EventsContainer);
        var leases = _cosmos.GetContainer(BeachTripCosmosOptions.DatabaseName, BeachTripCosmosOptions.LeasesContainer);

        _processor = events
            .GetChangeFeedProcessorBuilder<CosmosEventDocument>("beachtrip-projections", HandleChanges)
            .WithInstanceName($"worker-{Environment.MachineName}-{Guid.NewGuid():N}")
            .WithLeaseContainer(leases)
            .WithStartTime(DateTime.MinValue.ToUniversalTime())
            .Build();

        await _processor.StartAsync();
        _log.LogInformation("Projection change feed started.");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException) { }
        finally
        {
            if (_processor is not null)
                await _processor.StopAsync();
        }
    }

    private async Task HandleChanges(
        ChangeFeedProcessorContext context,
        IReadOnlyCollection<CosmosEventDocument> changes,
        CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var sp = scope.ServiceProvider;

        foreach (var doc in changes)
        {
            try
            {
                _log.LogDebug("Projecting {EventType} for {Aggregate} {Id} v{Version}",
                    doc.EventType, doc.AggregateType, doc.AggregateId, doc.Version);

                switch (doc.AggregateType)
                {
                    case nameof(Attendee):
                        await ProjectAttendee(sp, doc.AggregateId, ct);
                        break;
                    case nameof(Carpool):
                        await ProjectCarpool(sp, doc.AggregateId, ct);
                        break;
                    case nameof(Room):
                        await ProjectRoom(sp, doc.AggregateId, ct);
                        break;
                    case nameof(ParkingSpot):
                        await ProjectParkingSpot(sp, doc.AggregateId, ct);
                        break;
                    default:
                        _log.LogWarning("No projector for aggregate {AggregateType}", doc.AggregateType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed projecting {EventType} for {AggregateType} {AggregateId}",
                    doc.EventType, doc.AggregateType, doc.AggregateId);
                throw;
            }
        }
    }

    private async Task ProjectAttendee(IServiceProvider sp, string aggregateId, CancellationToken ct)
    {
        var repo = sp.GetRequiredService<IRepository<Attendee, AttendeeId>>();
        var attendee = await repo.Get(new AttendeeId(Guid.Parse(aggregateId)), ct);
        if (attendee is null) return;

        var view = new AttendeeView
        {
            Id = attendee.Id.ToString(),
            DisplayName = attendee.DisplayName,
            CarCapacity = attendee.Car?.Capacity,
            CarPreference = attendee.Car?.Preference.ToString(),
        };

        var container = _cosmos.GetContainer(BeachTripCosmosOptions.DatabaseName, BeachTripCosmosOptions.Views.Attendees);
        await container.UpsertItemAsync(view, new PartitionKey(view.Id), cancellationToken: ct);
    }

    private async Task ProjectCarpool(IServiceProvider sp, string aggregateId, CancellationToken ct)
    {
        var repo = sp.GetRequiredService<IRepository<Carpool, CarpoolId>>();
        var carpool = await repo.Get(new CarpoolId(Guid.Parse(aggregateId)), ct);
        if (carpool is null) return;

        var view = new CarpoolView
        {
            Id = carpool.Id.ToString(),
            DriverId = carpool.DriverId.ToString(),
            CarCapacity = carpool.CarCapacity,
            Preference = carpool.Preference.ToString(),
            Members = carpool.Members.Select(m => m.ToString()).ToList(),
            IsActive = carpool.IsActive,
        };

        var container = _cosmos.GetContainer(BeachTripCosmosOptions.DatabaseName, BeachTripCosmosOptions.Views.Carpools);
        await container.UpsertItemAsync(view, new PartitionKey(view.Id), cancellationToken: ct);
    }

    private async Task ProjectRoom(IServiceProvider sp, string aggregateId, CancellationToken ct)
    {
        var repo = sp.GetRequiredService<IRepository<Room, RoomId>>();
        var room = await repo.Get(new RoomId(Guid.Parse(aggregateId)), ct);
        if (room is null) return;

        var view = new RoomView
        {
            Id = room.Id.ToString(),
            Name = room.Name,
            Capacity = room.Capacity,
            IsLocked = room.IsLocked,
            Occupants = room.Occupants.Select(o => o.ToString()).ToList(),
            FreeSeats = room.FreeSeats,
        };

        var container = _cosmos.GetContainer(BeachTripCosmosOptions.DatabaseName, BeachTripCosmosOptions.Views.Rooms);
        await container.UpsertItemAsync(view, new PartitionKey(view.Id), cancellationToken: ct);
    }

    private async Task ProjectParkingSpot(IServiceProvider sp, string aggregateId, CancellationToken ct)
    {
        var repo = sp.GetRequiredService<IRepository<ParkingSpot, ParkingSpotId>>();
        var spot = await repo.Get(new ParkingSpotId(Guid.Parse(aggregateId)), ct);
        if (spot is null) return;

        var view = new ParkingSpotView
        {
            Id = spot.Id.ToString(),
            Name = spot.Name,
            Type = spot.Type.ToString(),
            IsLocked = spot.IsLocked,
            Claim = spot.CurrentClaim switch
            {
                ParkingClaim.Carpool c => new ParkingClaimDto { Kind = "Carpool", ClaimantId = c.CarpoolId.ToString() },
                ParkingClaim.Solo s => new ParkingClaimDto { Kind = "Solo", ClaimantId = s.AttendeeId.ToString() },
                _ => null,
            },
        };

        var container = _cosmos.GetContainer(BeachTripCosmosOptions.DatabaseName, BeachTripCosmosOptions.Views.ParkingSpots);
        await container.UpsertItemAsync(view, new PartitionKey(view.Id), cancellationToken: ct);
    }
}
