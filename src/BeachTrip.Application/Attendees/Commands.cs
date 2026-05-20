using BeachTrip.Domain;
using BeachTrip.Domain.Attendees;

namespace BeachTrip.Application.Attendees;

public sealed record RegisterAttendee(AttendeeId AttendeeId, string DisplayName);

public sealed record RenameAttendee(AttendeeId AttendeeId, string DisplayName);

public sealed record DeclareAttendeeCar(AttendeeId AttendeeId, int Capacity, ParkingPreference Preference);

public sealed record UpdateAttendeeCarPreference(AttendeeId AttendeeId, ParkingPreference Preference);

public sealed record DropAttendeeCar(AttendeeId AttendeeId);
