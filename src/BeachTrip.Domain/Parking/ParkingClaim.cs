using System.Text.Json.Serialization;

namespace BeachTrip.Domain.Parking;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(Carpool), "carpool")]
[JsonDerivedType(typeof(Solo), "solo")]
public abstract record ParkingClaim
{
    public sealed record Carpool(CarpoolId CarpoolId) : ParkingClaim;
    public sealed record Solo(AttendeeId AttendeeId) : ParkingClaim;
}
