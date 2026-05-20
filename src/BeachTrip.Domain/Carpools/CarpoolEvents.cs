using BeachTrip.Domain.Abstractions;
using BeachTrip.Domain.Attendees;

namespace BeachTrip.Domain.Carpools;

public sealed record CarpoolFormed(
    CarpoolId CarpoolId,
    AttendeeId DriverId,
    int CarCapacity,
    ParkingPreference Preference,
    IReadOnlyList<AttendeeId> Members) : DomainEvent;

public sealed record AttendeeJoinedCarpool(CarpoolId CarpoolId, AttendeeId AttendeeId) : DomainEvent;

public sealed record AttendeeLeftCarpool(CarpoolId CarpoolId, AttendeeId AttendeeId) : DomainEvent;

public sealed record CarpoolPreferenceChanged(CarpoolId CarpoolId, ParkingPreference Preference) : DomainEvent;

public sealed record CarpoolDisbanded(CarpoolId CarpoolId, string Reason) : DomainEvent;
