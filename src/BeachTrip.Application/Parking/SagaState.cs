using BeachTrip.Domain;
using BeachTrip.Domain.Attendees;
using BeachTrip.Domain.Parking;
using MassTransit;

namespace BeachTrip.Application.Parking;

public sealed class ParkingAllocationState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = "";
    public int Version { get; set; }

    public List<SagaSpot> Spots { get; set; } = new();
    public List<SagaClaim> Queue { get; set; } = new();
    public List<SagaSpotAssignment> Assignments { get; set; } = new();
}

public sealed record SagaSpot(ParkingSpotId SpotId, string Name, ParkingSpotType Type, bool IsLocked, ParkingClaim? LockedClaim);

public sealed record SagaClaim(ClaimKind Kind, Guid ClaimantId, ParkingPreference Preference)
{
    public static SagaClaim ForCarpool(CarpoolId id, ParkingPreference pref) => new(ClaimKind.Carpool, id.Value, pref);
    public static SagaClaim ForSolo(AttendeeId id, ParkingPreference pref) => new(ClaimKind.Solo, id.Value, pref);
}

public sealed record SagaSpotAssignment(ParkingSpotId SpotId, SagaClaim Claim);
