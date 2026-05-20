using BeachTrip.Infrastructure;
using BeachTrip.Infrastructure.Projections;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace BeachTrip.Web.Services;

public interface IViewStore
{
    Task<AttendeeView?> GetAttendee(string id, CancellationToken ct = default);
    Task<IReadOnlyList<AttendeeView>> ListAttendees(CancellationToken ct = default);
    Task<IReadOnlyList<CarpoolView>> ListCarpools(CancellationToken ct = default);
    Task<IReadOnlyList<RoomView>> ListRooms(CancellationToken ct = default);
    Task<IReadOnlyList<ParkingSpotView>> ListParkingSpots(CancellationToken ct = default);
}

public sealed class CosmosViewStore : IViewStore
{
    private readonly Container _attendees;
    private readonly Container _carpools;
    private readonly Container _rooms;
    private readonly Container _spots;

    public CosmosViewStore(CosmosClient client)
    {
        var db = BeachTripCosmosOptions.DatabaseName;
        _attendees = client.GetContainer(db, BeachTripCosmosOptions.Views.Attendees);
        _carpools  = client.GetContainer(db, BeachTripCosmosOptions.Views.Carpools);
        _rooms     = client.GetContainer(db, BeachTripCosmosOptions.Views.Rooms);
        _spots     = client.GetContainer(db, BeachTripCosmosOptions.Views.ParkingSpots);
    }

    public async Task<AttendeeView?> GetAttendee(string id, CancellationToken ct = default)
    {
        try
        {
            var response = await _attendees.ReadItemAsync<AttendeeView>(id, new PartitionKey(id), cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<IReadOnlyList<AttendeeView>> ListAttendees(CancellationToken ct = default) =>
        ListAll<AttendeeView>(_attendees, ct);

    public Task<IReadOnlyList<CarpoolView>> ListCarpools(CancellationToken ct = default) =>
        ListAll<CarpoolView>(_carpools, ct);

    public Task<IReadOnlyList<RoomView>> ListRooms(CancellationToken ct = default) =>
        ListAll<RoomView>(_rooms, ct);

    public Task<IReadOnlyList<ParkingSpotView>> ListParkingSpots(CancellationToken ct = default) =>
        ListAll<ParkingSpotView>(_spots, ct);

    private static async Task<IReadOnlyList<T>> ListAll<T>(Container container, CancellationToken ct)
    {
        try
        {
            var iterator = container.GetItemQueryIterator<T>("SELECT * FROM c");
            var results = new List<T>();
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(ct);
                results.AddRange(page);
            }
            return results;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // View container hasn't been provisioned yet (Web started before Worker seeded).
            // Tolerate empty; the projection feed will populate as events flow.
            return Array.Empty<T>();
        }
    }
}
