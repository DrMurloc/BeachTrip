using BeachTrip.Domain.Abstractions;

namespace BeachTrip.Domain.Attendees;

public sealed record AttendeeRegistered(AttendeeId AttendeeId, string DisplayName) : DomainEvent;

public sealed record AttendeeRenamed(AttendeeId AttendeeId, string DisplayName) : DomainEvent;

public sealed record AttendeeCarDeclared(AttendeeId AttendeeId, int Capacity, ParkingPreference Preference) : DomainEvent;

public sealed record AttendeeCarPreferenceUpdated(AttendeeId AttendeeId, ParkingPreference Preference) : DomainEvent;

public sealed record AttendeeCarDropped(AttendeeId AttendeeId) : DomainEvent;
