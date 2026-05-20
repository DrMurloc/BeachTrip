using BeachTrip.Domain.Abstractions;

namespace BeachTrip.Domain.Parking;

public sealed record ParkingSpotCreated(
    ParkingSpotId ParkingSpotId,
    string Name,
    ParkingSpotType Type,
    ParkingClaim? LockedClaim) : DomainEvent;

public sealed record ParkingSpotAssigned(ParkingSpotId ParkingSpotId, ParkingClaim Claim) : DomainEvent;

public sealed record ParkingSpotReleased(ParkingSpotId ParkingSpotId) : DomainEvent;

// Admin override: takes the spot out of the saga's auto-allocation pool by
// locking it to an explicit claim.
public sealed record ParkingSpotManuallyAssigned(ParkingSpotId ParkingSpotId, ParkingClaim Claim) : DomainEvent;

public sealed record ParkingSpotOverrideRemoved(ParkingSpotId ParkingSpotId) : DomainEvent;
