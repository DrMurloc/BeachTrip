using BeachTrip.Domain;
using BeachTrip.Domain.Abstractions;
using BeachTrip.Domain.Attendees;

namespace BeachTrip.Domain.Tests;

public sealed class AttendeeTests
{
    [Fact]
    public void Register_sets_identity_and_raises_event()
    {
        var id = AttendeeId.New();

        var attendee = Attendee.Register(id, "DrMurloc");

        Assert.Equal(id, attendee.Id);
        Assert.Equal("DrMurloc", attendee.DisplayName);
        Assert.IsType<AttendeeRegistered>(Assert.Single(attendee.UncommittedEvents));
        Assert.Equal(1, attendee.Version);
    }

    [Fact]
    public void Register_trims_whitespace()
    {
        var attendee = Attendee.Register(AttendeeId.New(), "  Iraiah  ");
        Assert.Equal("Iraiah", attendee.DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_rejects_blank_name(string name)
    {
        Assert.Throws<DomainException>(() => Attendee.Register(AttendeeId.New(), name));
    }

    [Fact]
    public void Rename_changes_display_name()
    {
        var attendee = Attendee.Register(AttendeeId.New(), "Old");
        attendee.ClearUncommittedEvents();

        attendee.Rename("New");

        Assert.Equal("New", attendee.DisplayName);
        Assert.IsType<AttendeeRenamed>(Assert.Single(attendee.UncommittedEvents));
    }

    [Fact]
    public void Rename_to_same_name_is_no_op()
    {
        var attendee = Attendee.Register(AttendeeId.New(), "Same");
        attendee.ClearUncommittedEvents();

        attendee.Rename("Same");

        Assert.Empty(attendee.UncommittedEvents);
    }

    [Fact]
    public void DeclareCar_sets_car_and_raises_event()
    {
        var attendee = Attendee.Register(AttendeeId.New(), "Driver");
        attendee.ClearUncommittedEvents();

        attendee.DeclareCar(4, ParkingPreference.Driveway);

        Assert.NotNull(attendee.Car);
        Assert.Equal(4, attendee.Car!.Capacity);
        Assert.Equal(ParkingPreference.Driveway, attendee.Car.Preference);
        Assert.IsType<AttendeeCarDeclared>(Assert.Single(attendee.UncommittedEvents));
    }

    [Fact]
    public void DeclareCar_rejects_zero_capacity()
    {
        var attendee = Attendee.Register(AttendeeId.New(), "Driver");
        Assert.Throws<DomainException>(() => attendee.DeclareCar(0, ParkingPreference.Driveway));
    }

    [Fact]
    public void DeclareCar_twice_throws()
    {
        var attendee = Attendee.Register(AttendeeId.New(), "Driver");
        attendee.DeclareCar(3, ParkingPreference.Driveway);
        Assert.Throws<DomainException>(() => attendee.DeclareCar(4, ParkingPreference.Street));
    }

    [Fact]
    public void UpdateCarPreference_without_car_throws()
    {
        var attendee = Attendee.Register(AttendeeId.New(), "NoCar");
        Assert.Throws<DomainException>(() => attendee.UpdateCarPreference(ParkingPreference.Driveway));
    }

    [Fact]
    public void UpdateCarPreference_to_same_value_is_no_op()
    {
        var attendee = Attendee.Register(AttendeeId.New(), "Driver");
        attendee.DeclareCar(4, ParkingPreference.Driveway);
        attendee.ClearUncommittedEvents();

        attendee.UpdateCarPreference(ParkingPreference.Driveway);

        Assert.Empty(attendee.UncommittedEvents);
    }

    [Fact]
    public void UpdateCarPreference_changes_value_and_raises_event()
    {
        var attendee = Attendee.Register(AttendeeId.New(), "Driver");
        attendee.DeclareCar(4, ParkingPreference.Driveway);
        attendee.ClearUncommittedEvents();

        attendee.UpdateCarPreference(ParkingPreference.Street);

        Assert.Equal(ParkingPreference.Street, attendee.Car!.Preference);
        Assert.IsType<AttendeeCarPreferenceUpdated>(Assert.Single(attendee.UncommittedEvents));
    }

    [Fact]
    public void DropCar_clears_car_and_raises_event()
    {
        var attendee = Attendee.Register(AttendeeId.New(), "Driver");
        attendee.DeclareCar(3, ParkingPreference.Driveway);
        attendee.ClearUncommittedEvents();

        attendee.DropCar();

        Assert.Null(attendee.Car);
        Assert.IsType<AttendeeCarDropped>(Assert.Single(attendee.UncommittedEvents));
    }

    [Fact]
    public void DropCar_without_car_is_no_op()
    {
        var attendee = Attendee.Register(AttendeeId.New(), "NoCar");
        attendee.ClearUncommittedEvents();

        attendee.DropCar();

        Assert.Empty(attendee.UncommittedEvents);
    }

    [Fact]
    public void Load_rehydrates_from_event_history()
    {
        var id = AttendeeId.New();
        var history = new DomainEvent[]
        {
            new AttendeeRegistered(id, "Original"),
            new AttendeeRenamed(id, "Renamed"),
            new AttendeeCarDeclared(id, 3, ParkingPreference.Driveway),
            new AttendeeCarPreferenceUpdated(id, ParkingPreference.Street),
        };

        var attendee = ReplayHelper.Replay<Attendee, AttendeeId>(history);

        Assert.Equal(id, attendee.Id);
        Assert.Equal("Renamed", attendee.DisplayName);
        Assert.Equal(3, attendee.Car!.Capacity);
        Assert.Equal(ParkingPreference.Street, attendee.Car.Preference);
        Assert.Equal(4, attendee.Version);
        Assert.Empty(attendee.UncommittedEvents);
    }
}
