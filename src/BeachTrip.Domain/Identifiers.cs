namespace BeachTrip.Domain;

public readonly record struct AttendeeId(Guid Value)
{
    public static AttendeeId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct CarpoolId(Guid Value)
{
    public static CarpoolId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct RoomId(Guid Value)
{
    public static RoomId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct ParkingSpotId(Guid Value)
{
    public static ParkingSpotId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
