using BeachTrip.Domain;
using BeachTrip.Domain.Parking;

namespace BeachTrip.Application.Parking;

// Admin-only commands that override the saga's automatic spot allocation. The host
// (Web in our setup) is responsible for gating who's allowed to send these.
public sealed record ManuallyAssignSpot(ParkingSpotId SpotId, ParkingClaim Claim);

public sealed record RemoveSpotOverride(ParkingSpotId SpotId);
