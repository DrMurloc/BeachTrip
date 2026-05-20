using BeachTrip.Application.Parking;
using BeachTrip.Domain;
using BeachTrip.Domain.Attendees;
using BeachTrip.Domain.Parking;

namespace BeachTrip.Application.Tests;

public sealed class ParkingAllocatorTests
{
    private static SagaSpot Spot(string name, ParkingSpotType type, bool locked = false) =>
        new(ParkingSpotId.New(), name, type, locked, null);

    [Fact]
    public void Empty_queue_yields_no_assignments()
    {
        var spots = new[] { Spot("Driveway-1", ParkingSpotType.Driveway) };
        var outcome = ParkingAllocator.Allocate(spots, Array.Empty<SagaClaim>());
        Assert.Empty(outcome.Assignments);
    }

    [Fact]
    public void Single_carpool_gets_a_spot()
    {
        var carpool = CarpoolId.New();
        var spots = new[] { Spot("Driveway-1", ParkingSpotType.Driveway) };
        var queue = new[] { SagaClaim.ForCarpool(carpool, ParkingPreference.Driveway) };

        var outcome = ParkingAllocator.Allocate(spots, queue);

        var assignment = Assert.Single(outcome.Assignments);
        Assert.Equal(ClaimKind.Carpool, assignment.Claim.Kind);
        Assert.Equal(carpool.Value, assignment.Claim.ClaimantId);
    }

    [Fact]
    public void Carpool_beats_solo_for_the_last_spot()
    {
        var spots = new[] { Spot("D1", ParkingSpotType.Driveway) };
        var solo = AttendeeId.New();
        var carpool = CarpoolId.New();
        var queue = new[]
        {
            SagaClaim.ForSolo(solo, ParkingPreference.Driveway),
            SagaClaim.ForCarpool(carpool, ParkingPreference.Driveway),
        };

        var outcome = ParkingAllocator.Allocate(spots, queue);

        var assignment = Assert.Single(outcome.Assignments);
        Assert.Equal(ClaimKind.Carpool, assignment.Claim.Kind);
        Assert.Equal(carpool.Value, assignment.Claim.ClaimantId);
    }

    [Fact]
    public void Driveway_preference_picks_driveway_when_available()
    {
        var spots = new[]
        {
            Spot("S1", ParkingSpotType.Street),
            Spot("D1", ParkingSpotType.Driveway),
        };
        var carpool = CarpoolId.New();
        var queue = new[] { SagaClaim.ForCarpool(carpool, ParkingPreference.Driveway) };

        var outcome = ParkingAllocator.Allocate(spots, queue);
        var assignment = Assert.Single(outcome.Assignments);
        Assert.Equal(ParkingSpotType.Driveway, spots.First(s => s.SpotId == assignment.SpotId).Type);
    }

    [Fact]
    public void Driveway_preference_falls_back_to_street_when_no_driveway_free()
    {
        var spots = new[] { Spot("S1", ParkingSpotType.Street) };
        var carpool = CarpoolId.New();
        var queue = new[] { SagaClaim.ForCarpool(carpool, ParkingPreference.Driveway) };

        var outcome = ParkingAllocator.Allocate(spots, queue);
        var assignment = Assert.Single(outcome.Assignments);
        Assert.Equal(ParkingSpotType.Street, spots.First(s => s.SpotId == assignment.SpotId).Type);
    }

    [Fact]
    public void Locked_spots_are_never_assigned()
    {
        var spots = new[]
        {
            Spot("Driveway-1", ParkingSpotType.Driveway, locked: true),
            Spot("Driveway-2", ParkingSpotType.Driveway),
        };
        var carpool = CarpoolId.New();
        var queue = new[] { SagaClaim.ForCarpool(carpool, ParkingPreference.Driveway) };

        var outcome = ParkingAllocator.Allocate(spots, queue);
        var assignment = Assert.Single(outcome.Assignments);
        Assert.Equal(spots[1].SpotId, assignment.SpotId);
    }

    [Fact]
    public void Two_carpools_and_one_solo_with_only_two_spots_leaves_solo_unmet()
    {
        var spots = new[]
        {
            Spot("D1", ParkingSpotType.Driveway),
            Spot("S1", ParkingSpotType.Street),
        };
        var c1 = CarpoolId.New();
        var c2 = CarpoolId.New();
        var s1 = AttendeeId.New();
        var queue = new[]
        {
            SagaClaim.ForCarpool(c1, ParkingPreference.Driveway),
            SagaClaim.ForCarpool(c2, ParkingPreference.Street),
            SagaClaim.ForSolo(s1, ParkingPreference.Driveway),
        };

        var outcome = ParkingAllocator.Allocate(spots, queue);

        Assert.Equal(2, outcome.Assignments.Count);
        Assert.All(outcome.Assignments, a => Assert.Equal(ClaimKind.Carpool, a.Claim.Kind));
    }

    [Fact]
    public void Solo_gets_remaining_spot_when_no_carpool_competes()
    {
        var spots = new[] { Spot("S1", ParkingSpotType.Street) };
        var solo = AttendeeId.New();
        var queue = new[] { SagaClaim.ForSolo(solo, ParkingPreference.Street) };

        var outcome = ParkingAllocator.Allocate(spots, queue);

        var assignment = Assert.Single(outcome.Assignments);
        Assert.Equal(ClaimKind.Solo, assignment.Claim.Kind);
        Assert.Equal(solo.Value, assignment.Claim.ClaimantId);
    }
}
