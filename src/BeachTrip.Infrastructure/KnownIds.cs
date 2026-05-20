namespace BeachTrip.Infrastructure;

// Stable IDs for the static catalog (rooms, parking spots) and the permanently
// claimed family. Used by CatalogSeeder so reruns don't duplicate the catalog.
public static class KnownIds
{
    public static readonly Guid DrMurloc = Guid.Parse("11111111-aaaa-1111-aaaa-111111111111");
    public static readonly Guid Iraiah   = Guid.Parse("22222222-aaaa-2222-aaaa-222222222222");
    public static readonly Guid Murky    = Guid.Parse("33333333-aaaa-3333-aaaa-333333333333");

    public static class Rooms
    {
        public static readonly Guid FirstFloorQueen   = Guid.Parse("a1000000-0000-0000-0000-000000000001");
        public static readonly Guid SecondFloorRight  = Guid.Parse("a1000000-0000-0000-0000-000000000002");
        public static readonly Guid SecondFloorLeft   = Guid.Parse("a1000000-0000-0000-0000-000000000003");
        public static readonly Guid ThirdFloorKing    = Guid.Parse("a1000000-0000-0000-0000-000000000004");
        public static readonly Guid ThirdFloorTwin    = Guid.Parse("a1000000-0000-0000-0000-000000000005");
        public static readonly Guid ThirdFloorAlcove  = Guid.Parse("a1000000-0000-0000-0000-000000000006");
    }

    public static class ParkingSpots
    {
        public static readonly Guid Driveway1 = Guid.Parse("b1000000-0000-0000-0000-000000000001");
        public static readonly Guid Driveway2 = Guid.Parse("b1000000-0000-0000-0000-000000000002");
        public static readonly Guid Driveway3 = Guid.Parse("b1000000-0000-0000-0000-000000000003");
        public static readonly Guid Driveway4 = Guid.Parse("b1000000-0000-0000-0000-000000000004");
        public static readonly Guid Street1   = Guid.Parse("b1000000-0000-0000-0000-000000000005");
        public static readonly Guid Street2   = Guid.Parse("b1000000-0000-0000-0000-000000000006");
    }
}
