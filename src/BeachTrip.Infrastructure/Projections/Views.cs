namespace BeachTrip.Infrastructure.Projections;

public sealed class AttendeeView
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int? CarCapacity { get; set; }
    public string? CarPreference { get; set; }
}

public sealed class CarpoolView
{
    public string Id { get; set; } = "";
    public string DriverId { get; set; } = "";
    public int CarCapacity { get; set; }
    public string Preference { get; set; } = "";
    public List<string> Members { get; set; } = new();
    public bool IsActive { get; set; }
}

public sealed class RoomView
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Capacity { get; set; }
    public bool IsLocked { get; set; }
    public List<string> Occupants { get; set; } = new();
    public int FreeSeats { get; set; }
}

public sealed class ParkingSpotView
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsLocked { get; set; }
    public ParkingClaimDto? Claim { get; set; }
}

public sealed class ParkingClaimDto
{
    public string Kind { get; set; } = "";
    public string ClaimantId { get; set; } = "";
}
