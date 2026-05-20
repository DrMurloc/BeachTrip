using BeachTrip.Domain;
using BeachTrip.Domain.Attendees;
using BeachTrip.Domain.Parking;

namespace BeachTrip.Application.Parking;

// Pure allocation algorithm. Given inventory and a queue of claims, decide who parks where.
// Carpool claims take precedence over solo claims. Within a priority tier, queue order
// (first-come-first-served) breaks ties. A claim with a typed preference (Driveway/Street)
// gets a matching spot if one is free; otherwise it falls back to any free spot.
public static class ParkingAllocator
{
    public static AllocationOutcome Allocate(IReadOnlyList<SagaSpot> spots, IReadOnlyList<SagaClaim> queue)
    {
        var assignments = new List<SagaSpotAssignment>();
        var remaining = new HashSet<ParkingSpotId>(spots.Where(s => !s.IsLocked).Select(s => s.SpotId));

        IEnumerable<SagaClaim> ordered = queue.Where(c => c.Kind == ClaimKind.Carpool)
            .Concat(queue.Where(c => c.Kind == ClaimKind.Solo));

        foreach (var claim in ordered)
        {
            var preferredType = PreferredType(claim.Preference);
            SagaSpot? chosen = null;
            if (preferredType is not null)
                chosen = spots.FirstOrDefault(s => remaining.Contains(s.SpotId) && s.Type == preferredType);
            chosen ??= spots.FirstOrDefault(s => remaining.Contains(s.SpotId));

            if (chosen is null) continue;

            remaining.Remove(chosen.SpotId);
            assignments.Add(new SagaSpotAssignment(chosen.SpotId, claim));
        }

        return new AllocationOutcome(assignments);
    }

    private static ParkingSpotType? PreferredType(ParkingPreference preference) => preference switch
    {
        ParkingPreference.Driveway => ParkingSpotType.Driveway,
        ParkingPreference.Street => ParkingSpotType.Street,
        _ => null,
    };
}

public sealed record AllocationOutcome(IReadOnlyList<SagaSpotAssignment> Assignments);
