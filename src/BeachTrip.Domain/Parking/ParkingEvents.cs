using BeachTrip.Domain.Abstractions;

namespace BeachTrip.Domain.Parking;

public sealed record ParkingSpotCreated(
    ParkingSpotId ParkingSpotId,
    string Name,
    ParkingSpotType Type,
    ParkingClaim? LockedClaim) : DomainEvent;

public sealed record ParkingSpotAssigned(ParkingSpotId ParkingSpotId, ParkingClaim Claim) : DomainEvent;

public sealed record ParkingSpotReleased(ParkingSpotId ParkingSpotId) : DomainEvent;
