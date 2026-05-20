using BeachTrip.Domain.Abstractions;

namespace BeachTrip.Domain.Rooms;

public sealed class Room : AggregateRoot<RoomId>
{
    private readonly List<AttendeeId> _occupants = new();

    public string Name { get; private set; } = "";
    public int Capacity { get; private set; }
    public bool IsLocked { get; private set; }
    public IReadOnlyList<AttendeeId> Occupants => _occupants;
    public int FreeSeats => Math.Max(0, Capacity - _occupants.Count);

    private Room() { }

    public static Room Create(
        RoomId id,
        string name,
        int capacity,
        bool isLocked = false,
        IEnumerable<AttendeeId>? initialOccupants = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Room name is required.");
        if (capacity < 1)
            throw new DomainException("Room capacity must be at least 1.");

        var seeds = initialOccupants?.Distinct().ToList() ?? new List<AttendeeId>();
        if (seeds.Count > capacity)
            throw new DomainException($"Initial occupants ({seeds.Count}) exceed capacity ({capacity}).");

        var room = new Room();
        room.Raise(new RoomCreated(id, name.Trim(), capacity, isLocked, seeds));
        return room;
    }

    public void AddOccupant(AttendeeId attendee)
    {
        if (IsLocked)
            throw new DomainException($"Room '{Name}' is locked; occupants cannot change.");
        if (_occupants.Contains(attendee)) return;
        if (_occupants.Count + 1 > Capacity)
            throw new DomainException($"Room '{Name}' is at capacity.");
        Raise(new AttendeeJoinedRoom(Id, attendee));
    }

    public void RemoveOccupant(AttendeeId attendee)
    {
        if (IsLocked)
            throw new DomainException($"Room '{Name}' is locked; occupants cannot change.");
        if (!_occupants.Contains(attendee)) return;
        Raise(new AttendeeLeftRoom(Id, attendee));
    }

    protected override void Apply(DomainEvent @event)
    {
        switch (@event)
        {
            case RoomCreated e:
                Id = e.RoomId;
                Name = e.Name;
                Capacity = e.Capacity;
                IsLocked = e.IsLocked;
                _occupants.Clear();
                _occupants.AddRange(e.InitialOccupants);
                break;
            case AttendeeJoinedRoom e:
                if (!_occupants.Contains(e.AttendeeId))
                    _occupants.Add(e.AttendeeId);
                break;
            case AttendeeLeftRoom e:
                _occupants.Remove(e.AttendeeId);
                break;
        }
    }
}
