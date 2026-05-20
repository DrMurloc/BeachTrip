using BeachTrip.Domain.Abstractions;

namespace BeachTrip.Domain.Attendees;

public sealed class Attendee : AggregateRoot<AttendeeId>
{
    public string DisplayName { get; private set; } = "";
    public Car? Car { get; private set; }
    public bool HasCar => Car is not null;

    private Attendee() { }

    public static Attendee Register(AttendeeId id, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("DisplayName is required.");

        var attendee = new Attendee();
        attendee.Raise(new AttendeeRegistered(id, displayName.Trim()));
        return attendee;
    }

    public void Rename(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("DisplayName is required.");
        var trimmed = displayName.Trim();
        if (DisplayName == trimmed) return;
        Raise(new AttendeeRenamed(Id, trimmed));
    }

    public void DeclareCar(int capacity, ParkingPreference preference)
    {
        if (HasCar)
            throw new DomainException("Attendee already has a declared car. Drop it first or update its preference.");
        _ = new Car(capacity, preference);
        Raise(new AttendeeCarDeclared(Id, capacity, preference));
    }

    public void UpdateCarPreference(ParkingPreference preference)
    {
        if (!HasCar)
            throw new DomainException("Cannot update parking preference: no car declared.");
        if (Car!.Preference == preference) return;
        Raise(new AttendeeCarPreferenceUpdated(Id, preference));
    }

    public void DropCar()
    {
        if (!HasCar) return;
        Raise(new AttendeeCarDropped(Id));
    }

    protected override void Apply(DomainEvent @event)
    {
        switch (@event)
        {
            case AttendeeRegistered e:
                Id = e.AttendeeId;
                DisplayName = e.DisplayName;
                break;
            case AttendeeRenamed e:
                DisplayName = e.DisplayName;
                break;
            case AttendeeCarDeclared e:
                Car = new Car(e.Capacity, e.Preference);
                break;
            case AttendeeCarPreferenceUpdated e:
                Car = Car! with { Preference = e.Preference };
                break;
            case AttendeeCarDropped:
                Car = null;
                break;
        }
    }
}
