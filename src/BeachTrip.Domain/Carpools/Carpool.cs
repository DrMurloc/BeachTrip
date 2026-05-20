using BeachTrip.Domain.Abstractions;
using BeachTrip.Domain.Attendees;

namespace BeachTrip.Domain.Carpools;

public sealed class Carpool : AggregateRoot<CarpoolId>
{
    private readonly List<AttendeeId> _members = new();

    public AttendeeId DriverId { get; private set; }
    public int CarCapacity { get; private set; }
    public ParkingPreference Preference { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyList<AttendeeId> Members => _members;

    private Carpool() { }

    public static Carpool Form(
        CarpoolId id,
        AttendeeId driverId,
        int carCapacity,
        ParkingPreference preference,
        IEnumerable<AttendeeId>? initialPassengers = null)
    {
        if (carCapacity < 1)
            throw new DomainException("Carpool requires a car with capacity >= 1.");

        var members = new List<AttendeeId> { driverId };
        if (initialPassengers is not null)
        {
            foreach (var passenger in initialPassengers.Distinct())
                if (passenger != driverId)
                    members.Add(passenger);
        }

        if (members.Count > carCapacity)
            throw new DomainException($"Carpool has {members.Count} members but car capacity is {carCapacity}.");

        var carpool = new Carpool();
        carpool.Raise(new CarpoolFormed(id, driverId, carCapacity, preference, members.ToList()));
        return carpool;
    }

    public void AddMember(AttendeeId attendee)
    {
        EnsureActive();
        if (_members.Contains(attendee)) return;
        if (_members.Count + 1 > CarCapacity)
            throw new DomainException("Car is at capacity.");
        Raise(new AttendeeJoinedCarpool(Id, attendee));
    }

    public void RemoveMember(AttendeeId attendee)
    {
        EnsureActive();
        if (!_members.Contains(attendee)) return;
        if (attendee == DriverId)
            throw new DomainException("Cannot remove the driver. Disband the carpool instead.");
        Raise(new AttendeeLeftCarpool(Id, attendee));
    }

    public void ChangePreference(ParkingPreference preference)
    {
        EnsureActive();
        if (Preference == preference) return;
        Raise(new CarpoolPreferenceChanged(Id, preference));
    }

    public void Disband(string reason)
    {
        if (!IsActive) return;
        Raise(new CarpoolDisbanded(Id, reason));
    }

    private void EnsureActive()
    {
        if (!IsActive)
            throw new DomainException("Carpool is disbanded.");
    }

    protected override void Apply(DomainEvent @event)
    {
        switch (@event)
        {
            case CarpoolFormed e:
                Id = e.CarpoolId;
                DriverId = e.DriverId;
                CarCapacity = e.CarCapacity;
                Preference = e.Preference;
                _members.Clear();
                _members.AddRange(e.Members);
                IsActive = true;
                break;
            case AttendeeJoinedCarpool e:
                if (!_members.Contains(e.AttendeeId))
                    _members.Add(e.AttendeeId);
                break;
            case AttendeeLeftCarpool e:
                _members.Remove(e.AttendeeId);
                break;
            case CarpoolPreferenceChanged e:
                Preference = e.Preference;
                break;
            case CarpoolDisbanded:
                IsActive = false;
                break;
        }
    }
}
