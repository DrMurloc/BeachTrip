using BeachTrip.Domain;
using BeachTrip.Domain.Abstractions;
using BeachTrip.Domain.Attendees;
using BeachTrip.Domain.Carpools;

namespace BeachTrip.Domain.Tests;

public sealed class CarpoolTests
{
    private static readonly AttendeeId Driver = AttendeeId.New();
    private static readonly AttendeeId Passenger1 = AttendeeId.New();
    private static readonly AttendeeId Passenger2 = AttendeeId.New();

    [Fact]
    public void Form_with_driver_plus_passenger_succeeds()
    {
        var id = CarpoolId.New();
        var carpool = Carpool.Form(id, Driver, carCapacity: 3, ParkingPreference.Driveway, new[] { Passenger1 });

        Assert.Equal(id, carpool.Id);
        Assert.Equal(Driver, carpool.DriverId);
        Assert.True(carpool.IsActive);
        Assert.Equal(2, carpool.Members.Count);
        Assert.IsType<CarpoolFormed>(Assert.Single(carpool.UncommittedEvents));
    }

    [Fact]
    public void Form_with_only_driver_throws()
    {
        Assert.Throws<DomainException>(() =>
            Carpool.Form(CarpoolId.New(), Driver, carCapacity: 4, ParkingPreference.None));
    }

    [Fact]
    public void Form_rejects_car_capacity_below_two()
    {
        Assert.Throws<DomainException>(() =>
            Carpool.Form(CarpoolId.New(), Driver, carCapacity: 1, ParkingPreference.None, new[] { Passenger1 }));
    }

    [Fact]
    public void Form_rejects_members_exceeding_capacity()
    {
        Assert.Throws<DomainException>(() =>
            Carpool.Form(CarpoolId.New(), Driver, carCapacity: 2, ParkingPreference.None, new[] { Passenger1, Passenger2 }));
    }

    [Fact]
    public void Form_deduplicates_driver_in_passenger_list()
    {
        var carpool = Carpool.Form(CarpoolId.New(), Driver, 3, ParkingPreference.None, new[] { Driver, Passenger1 });
        Assert.Equal(2, carpool.Members.Count);
    }

    [Fact]
    public void AddMember_raises_event_and_grows_membership()
    {
        var carpool = Carpool.Form(CarpoolId.New(), Driver, 3, ParkingPreference.None, new[] { Passenger1 });
        carpool.ClearUncommittedEvents();

        carpool.AddMember(Passenger2);

        Assert.Contains(Passenger2, carpool.Members);
        Assert.IsType<AttendeeJoinedCarpool>(Assert.Single(carpool.UncommittedEvents));
    }

    [Fact]
    public void AddMember_beyond_capacity_throws()
    {
        var carpool = Carpool.Form(CarpoolId.New(), Driver, 2, ParkingPreference.None, new[] { Passenger1 });
        Assert.Throws<DomainException>(() => carpool.AddMember(Passenger2));
    }

    [Fact]
    public void RemoveMember_of_driver_throws()
    {
        var carpool = Carpool.Form(CarpoolId.New(), Driver, 3, ParkingPreference.None, new[] { Passenger1 });
        Assert.Throws<DomainException>(() => carpool.RemoveMember(Driver));
    }

    [Fact]
    public void RemoveMember_dropping_below_minimum_auto_disbands()
    {
        var carpool = Carpool.Form(CarpoolId.New(), Driver, 3, ParkingPreference.None, new[] { Passenger1 });
        carpool.ClearUncommittedEvents();

        carpool.RemoveMember(Passenger1);

        Assert.False(carpool.IsActive);
        Assert.Equal(2, carpool.UncommittedEvents.Count);
        Assert.IsType<AttendeeLeftCarpool>(carpool.UncommittedEvents[0]);
        Assert.IsType<CarpoolDisbanded>(carpool.UncommittedEvents[1]);
    }

    [Fact]
    public void ChangePreference_raises_event_only_on_change()
    {
        var carpool = Carpool.Form(CarpoolId.New(), Driver, 3, ParkingPreference.Driveway, new[] { Passenger1 });
        carpool.ClearUncommittedEvents();

        carpool.ChangePreference(ParkingPreference.Driveway);
        Assert.Empty(carpool.UncommittedEvents);

        carpool.ChangePreference(ParkingPreference.Street);
        Assert.IsType<CarpoolPreferenceChanged>(Assert.Single(carpool.UncommittedEvents));
    }

    [Fact]
    public void Disband_sets_inactive_and_blocks_further_writes()
    {
        var carpool = Carpool.Form(CarpoolId.New(), Driver, 3, ParkingPreference.None, new[] { Passenger1 });
        carpool.Disband("Manually disbanded for the test");

        Assert.False(carpool.IsActive);
        Assert.Throws<DomainException>(() => carpool.AddMember(Passenger2));
        Assert.Throws<DomainException>(() => carpool.ChangePreference(ParkingPreference.Street));
    }
}
