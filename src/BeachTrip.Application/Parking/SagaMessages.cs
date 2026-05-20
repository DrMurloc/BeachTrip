using BeachTrip.Domain;
using BeachTrip.Domain.Attendees;
using BeachTrip.Domain.Parking;

namespace BeachTrip.Application.Parking;

public sealed record SpotSeed(ParkingSpotId Id, string Name, ParkingSpotType Type, bool IsLocked, ParkingClaim? LockedClaim);

// Inbound — events the saga consumes.
public sealed record SeedParkingInventory(Guid CorrelationId, IReadOnlyList<SpotSeed> Spots);
public sealed record CarpoolWantsParking(Guid CorrelationId, CarpoolId CarpoolId, ParkingPreference Preference);
public sealed record CarpoolReleasesParking(Guid CorrelationId, CarpoolId CarpoolId);
public sealed record SoloDriverWantsParking(Guid CorrelationId, AttendeeId AttendeeId, ParkingPreference Preference);
public sealed record SoloDriverReleasesParking(Guid CorrelationId, AttendeeId AttendeeId);
public sealed record SpotTakenOutOfPool(Guid CorrelationId, ParkingSpotId SpotId);
public sealed record SpotReturnedToPool(Guid CorrelationId, ParkingSpotId SpotId);

// Outbound — events the saga publishes after each reallocation.
public sealed record ParkingSpotAllocated(ParkingSpotId SpotId, ParkingClaim Claim);
public sealed record ParkingSpotReclaimed(ParkingSpotId SpotId);
public sealed record SoloDriverBumped(AttendeeId AttendeeId);
public sealed record ClaimUnmet(ClaimKind Kind, Guid ClaimantId, ParkingPreference Preference);

public enum ClaimKind { Carpool = 1, Solo = 2 }
