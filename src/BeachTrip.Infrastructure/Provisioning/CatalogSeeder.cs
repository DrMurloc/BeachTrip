using BeachTrip.Application.Abstractions;
using BeachTrip.Domain;
using BeachTrip.Domain.Attendees;
using BeachTrip.Domain.Parking;
using BeachTrip.Domain.Rooms;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BeachTrip.Infrastructure.Provisioning;

// Idempotent startup seeder. Ensures the DB and containers exist, then writes the
// static catalog (6 rooms + 6 parking spots) and DrMurloc's family if they're missing.
public sealed class CatalogSeeder : IHostedService
{
    private readonly CosmosClient _cosmos;
    private readonly IRepository<Attendee, AttendeeId> _attendees;
    private readonly IRepository<Room, RoomId> _rooms;
    private readonly IRepository<ParkingSpot, ParkingSpotId> _spots;
    private readonly ILogger<CatalogSeeder> _log;

    public CatalogSeeder(
        CosmosClient cosmos,
        IRepository<Attendee, AttendeeId> attendees,
        IRepository<Room, RoomId> rooms,
        IRepository<ParkingSpot, ParkingSpotId> spots,
        ILogger<CatalogSeeder> log)
    {
        _cosmos = cosmos;
        _attendees = attendees;
        _rooms = rooms;
        _spots = spots;
        _log = log;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _log.LogInformation("Provisioning Cosmos database and containers...");
        await CosmosProvisioner.EnsureProvisionedAsync(_cosmos, cancellationToken, _log);
        _log.LogInformation("Provisioning complete. Seeding catalog...");

        await SeedFamily(cancellationToken);
        await SeedRooms(cancellationToken);
        await SeedParkingSpots(cancellationToken);

        _log.LogInformation("Catalog seeded.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedFamily(CancellationToken ct)
    {
        await EnsureAttendee(KnownIds.DrMurloc, "DrMurloc", car: (3, ParkingPreference.Driveway), ct);
        await EnsureAttendee(KnownIds.Iraiah, "Iraiah", car: null, ct);
        await EnsureAttendee(KnownIds.Murky, "Murky", car: null, ct);
    }

    private async Task EnsureAttendee(Guid id, string displayName, (int capacity, ParkingPreference pref)? car, CancellationToken ct)
    {
        var attendeeId = new AttendeeId(id);
        if (await _attendees.Get(attendeeId, ct) is not null) return;

        var attendee = Attendee.Register(attendeeId, displayName);
        if (car is not null)
            attendee.DeclareCar(car.Value.capacity, car.Value.pref);
        await _attendees.Save(attendee, ct);
        _log.LogInformation("Seeded attendee {Name} ({Id})", displayName, id);
    }

    private async Task SeedRooms(CancellationToken ct)
    {
        var familyOccupants = new[]
        {
            new AttendeeId(KnownIds.DrMurloc),
            new AttendeeId(KnownIds.Iraiah),
            new AttendeeId(KnownIds.Murky),
        };

        await EnsureRoom(KnownIds.Rooms.FirstFloorQueen,  "1F Queen",        capacity: 3, locked: false, occupants: null, ct);
        await EnsureRoom(KnownIds.Rooms.SecondFloorRight, "2F Right",        capacity: 3, locked: true,  occupants: familyOccupants, ct);
        await EnsureRoom(KnownIds.Rooms.SecondFloorLeft,  "2F Left",         capacity: 5, locked: false, occupants: null, ct);
        await EnsureRoom(KnownIds.Rooms.ThirdFloorKing,   "3F King",         capacity: 7, locked: false, occupants: null, ct);
        await EnsureRoom(KnownIds.Rooms.ThirdFloorTwin,   "3F Double Twin",  capacity: 3, locked: false, occupants: null, ct);
        await EnsureRoom(KnownIds.Rooms.ThirdFloorAlcove, "3F Alcove",       capacity: 3, locked: false, occupants: null, ct);
    }

    private async Task EnsureRoom(Guid id, string name, int capacity, bool locked, IEnumerable<AttendeeId>? occupants, CancellationToken ct)
    {
        var roomId = new RoomId(id);
        if (await _rooms.Get(roomId, ct) is not null) return;

        var room = Room.Create(roomId, name, capacity, locked, occupants);
        await _rooms.Save(room, ct);
        _log.LogInformation("Seeded room {Name} cap={Capacity} locked={Locked}", name, capacity, locked);
    }

    private async Task SeedParkingSpots(CancellationToken ct)
    {
        // No pre-seeded locks — DrMurloc reserves spots via the admin assign UI.
        await EnsureSpot(KnownIds.ParkingSpots.Driveway1, "Driveway-1", ParkingSpotType.Driveway, lockedClaim: null, ct);
        await EnsureSpot(KnownIds.ParkingSpots.Driveway2, "Driveway-2", ParkingSpotType.Driveway, lockedClaim: null, ct);
        await EnsureSpot(KnownIds.ParkingSpots.Driveway3, "Driveway-3", ParkingSpotType.Driveway, lockedClaim: null, ct);
        await EnsureSpot(KnownIds.ParkingSpots.Driveway4, "Driveway-4", ParkingSpotType.Driveway, lockedClaim: null, ct);
        await EnsureSpot(KnownIds.ParkingSpots.Street1,   "Street-1",   ParkingSpotType.Street,   lockedClaim: null, ct);
        await EnsureSpot(KnownIds.ParkingSpots.Street2,   "Street-2",   ParkingSpotType.Street,   lockedClaim: null, ct);
    }

    private async Task EnsureSpot(Guid id, string name, ParkingSpotType type, ParkingClaim? lockedClaim, CancellationToken ct)
    {
        var spotId = new ParkingSpotId(id);
        if (await _spots.Get(spotId, ct) is not null) return;

        var spot = ParkingSpot.Create(spotId, name, type, lockedClaim);
        await _spots.Save(spot, ct);
        _log.LogInformation("Seeded parking spot {Name} type={Type} locked={Locked}", name, type, lockedClaim is not null);
    }
}
