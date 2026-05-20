namespace BeachTrip.Infrastructure;

public sealed class BeachTripCosmosOptions
{
    public const string DatabaseName = "beachtrip";
    public const string EventsContainer = "events";
    public const string SagasContainer = "sagas";
    public const string LeasesContainer = "projection-leases";

    public static class Views
    {
        public const string Attendees = "view-attendees";
        public const string Carpools = "view-carpools";
        public const string Rooms = "view-rooms";
        public const string ParkingSpots = "view-parking-spots";
        public const string ParkingAllocation = "view-parking-allocation";
    }
}
