using BeachTrip.Domain.Abstractions;

namespace BeachTrip.Domain.Attendees;

public sealed record Car
{
    public int Capacity { get; init; }
    public ParkingPreference Preference { get; init; }

    public Car(int capacity, ParkingPreference preference)
    {
        if (capacity < 1)
            throw new DomainException("Car capacity must be at least 1.");
        Capacity = capacity;
        Preference = preference;
    }
}
