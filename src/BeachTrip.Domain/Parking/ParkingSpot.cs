using BeachTrip.Domain.Abstractions;

namespace BeachTrip.Domain.Parking;

public sealed class ParkingSpot : AggregateRoot<ParkingSpotId>
{
    public string Name { get; private set; } = "";
    public ParkingSpotType Type { get; private set; }
    public bool IsLocked { get; private set; }
    public ParkingClaim? CurrentClaim { get; private set; }
    public bool IsOccupied => CurrentClaim is not null;

    private ParkingSpot() { }

    public static ParkingSpot Create(
        ParkingSpotId id,
        string name,
        ParkingSpotType type,
        ParkingClaim? lockedClaim = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Parking spot name is required.");

        var spot = new ParkingSpot();
        spot.Raise(new ParkingSpotCreated(id, name.Trim(), type, lockedClaim));
        return spot;
    }

    // Saga-driven assignment. Refuses if the spot is locked (created locked, or admin-overridden).
    public void AssignToCarpool(CarpoolId carpoolId)
    {
        EnsureUnlocked();
        var newClaim = new ParkingClaim.Carpool(carpoolId);
        if (CurrentClaim == newClaim) return;
        Raise(new ParkingSpotAssigned(Id, newClaim));
    }

    public void AssignToSoloDriver(AttendeeId attendeeId)
    {
        EnsureUnlocked();
        var newClaim = new ParkingClaim.Solo(attendeeId);
        if (CurrentClaim == newClaim) return;
        Raise(new ParkingSpotAssigned(Id, newClaim));
    }

    public void Release()
    {
        EnsureUnlocked();
        if (CurrentClaim is null) return;
        Raise(new ParkingSpotReleased(Id));
    }

    // Admin override. Bypasses the lock guard so admins can reassign at will.
    // Sets IsLocked=true so the saga's allocator skips this spot afterward.
    public void OverrideTo(ParkingClaim claim)
    {
        if (IsLocked && CurrentClaim == claim) return;
        Raise(new ParkingSpotManuallyAssigned(Id, claim));
    }

    public void RemoveOverride()
    {
        if (!IsLocked) return;
        Raise(new ParkingSpotOverrideRemoved(Id));
    }

    private void EnsureUnlocked()
    {
        if (IsLocked)
            throw new DomainException($"Parking spot '{Name}' is locked; assignment cannot change.");
    }

    protected override void Apply(DomainEvent @event)
    {
        switch (@event)
        {
            case ParkingSpotCreated e:
                Id = e.ParkingSpotId;
                Name = e.Name;
                Type = e.Type;
                IsLocked = e.LockedClaim is not null;
                CurrentClaim = e.LockedClaim;
                break;
            case ParkingSpotAssigned e:
                CurrentClaim = e.Claim;
                break;
            case ParkingSpotReleased:
                CurrentClaim = null;
                break;
            case ParkingSpotManuallyAssigned e:
                IsLocked = true;
                CurrentClaim = e.Claim;
                break;
            case ParkingSpotOverrideRemoved:
                IsLocked = false;
                CurrentClaim = null;
                break;
        }
    }
}
