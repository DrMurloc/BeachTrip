namespace BeachTrip.Domain.Parking;

public abstract record ParkingClaim
{
    public sealed record Carpool(CarpoolId CarpoolId) : ParkingClaim;
    public sealed record Solo(AttendeeId AttendeeId) : ParkingClaim;
}
