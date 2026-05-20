using BeachTrip.Domain;
using BeachTrip.Domain.Abstractions;
using BeachTrip.Domain.Parking;

namespace BeachTrip.Domain.Tests;

public sealed class ParkingSpotTests
{
    [Fact]
    public void Create_unlocked_spot_is_unoccupied()
    {
        var spot = ParkingSpot.Create(ParkingSpotId.New(), "Driveway-1", ParkingSpotType.Driveway);
        Assert.False(spot.IsOccupied);
        Assert.False(spot.IsLocked);
    }

    [Fact]
    public void Create_with_locked_claim_marks_as_locked_and_occupied()
    {
        var carpoolId = CarpoolId.New();
        var spot = ParkingSpot.Create(
            ParkingSpotId.New(),
            "Driveway-1",
            ParkingSpotType.Driveway,
            new ParkingClaim.Carpool(carpoolId));

        Assert.True(spot.IsLocked);
        Assert.True(spot.IsOccupied);
        Assert.Equal(new ParkingClaim.Carpool(carpoolId), spot.CurrentClaim);
    }

    [Fact]
    public void Create_rejects_blank_name()
    {
        Assert.Throws<DomainException>(() =>
            ParkingSpot.Create(ParkingSpotId.New(), "  ", ParkingSpotType.Street));
    }

    [Fact]
    public void AssignToCarpool_raises_event()
    {
        var spot = ParkingSpot.Create(ParkingSpotId.New(), "Spot", ParkingSpotType.Driveway);
        spot.ClearUncommittedEvents();
        var carpool = CarpoolId.New();

        spot.AssignToCarpool(carpool);

        Assert.Equal(new ParkingClaim.Carpool(carpool), spot.CurrentClaim);
        Assert.IsType<ParkingSpotAssigned>(Assert.Single(spot.UncommittedEvents));
    }

    [Fact]
    public void AssignToCarpool_idempotent_for_same_carpool()
    {
        var spot = ParkingSpot.Create(ParkingSpotId.New(), "Spot", ParkingSpotType.Driveway);
        var carpool = CarpoolId.New();
        spot.AssignToCarpool(carpool);
        spot.ClearUncommittedEvents();

        spot.AssignToCarpool(carpool);

        Assert.Empty(spot.UncommittedEvents);
    }

    [Fact]
    public void Release_clears_claim()
    {
        var spot = ParkingSpot.Create(ParkingSpotId.New(), "Spot", ParkingSpotType.Driveway);
        spot.AssignToSoloDriver(AttendeeId.New());
        spot.ClearUncommittedEvents();

        spot.Release();

        Assert.False(spot.IsOccupied);
        Assert.IsType<ParkingSpotReleased>(Assert.Single(spot.UncommittedEvents));
    }

    [Fact]
    public void Locked_spot_blocks_assignment_and_release()
    {
        var carpool = CarpoolId.New();
        var spot = ParkingSpot.Create(
            ParkingSpotId.New(),
            "Reserved",
            ParkingSpotType.Driveway,
            new ParkingClaim.Carpool(carpool));

        Assert.Throws<DomainException>(() => spot.AssignToCarpool(CarpoolId.New()));
        Assert.Throws<DomainException>(() => spot.AssignToSoloDriver(AttendeeId.New()));
        Assert.Throws<DomainException>(() => spot.Release());
    }

    [Fact]
    public void Switching_assignment_from_solo_to_carpool_raises_one_event()
    {
        var spot = ParkingSpot.Create(ParkingSpotId.New(), "Spot", ParkingSpotType.Street);
        spot.AssignToSoloDriver(AttendeeId.New());
        spot.ClearUncommittedEvents();

        spot.AssignToCarpool(CarpoolId.New());

        Assert.IsType<ParkingSpotAssigned>(Assert.Single(spot.UncommittedEvents));
    }
}
