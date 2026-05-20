namespace BeachTrip.Infrastructure.Projections;

// Published after the projector upserts a view document. Web subscribes so live
// circuits can refresh without polling. AggregateType is the .NET type name
// (Attendee / Carpool / Room / ParkingSpot); AggregateId is the GUID as a string.
public sealed record ViewUpdated(string AggregateType, string AggregateId);
