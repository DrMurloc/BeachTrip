using BeachTrip.Domain;
using BeachTrip.Domain.Attendees;

namespace BeachTrip.Application.Carpools;

public sealed record FormCarpool(
    CarpoolId CarpoolId,
    AttendeeId DriverId,
    int CarCapacity,
    ParkingPreference Preference,
    IReadOnlyList<AttendeeId>? InitialPassengers = null);

public sealed record JoinCarpool(CarpoolId CarpoolId, AttendeeId AttendeeId);

public sealed record LeaveCarpool(CarpoolId CarpoolId, AttendeeId AttendeeId);

public sealed record ChangeCarpoolPreference(CarpoolId CarpoolId, ParkingPreference Preference);

public sealed record DisbandCarpool(CarpoolId CarpoolId, string Reason);
