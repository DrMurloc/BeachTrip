using BeachTrip.Domain;
using BeachTrip.Domain.Abstractions;
using BeachTrip.Domain.Rooms;

namespace BeachTrip.Domain.Tests;

public sealed class RoomTests
{
    [Fact]
    public void Create_sets_state_and_raises_event()
    {
        var id = RoomId.New();
        var room = Room.Create(id, "3F King", capacity: 7);

        Assert.Equal(id, room.Id);
        Assert.Equal("3F King", room.Name);
        Assert.Equal(7, room.Capacity);
        Assert.Equal(7, room.FreeSeats);
        Assert.False(room.IsLocked);
        Assert.IsType<RoomCreated>(Assert.Single(room.UncommittedEvents));
    }

    [Fact]
    public void Create_with_initial_occupants_reduces_free_seats()
    {
        var family = new[] { AttendeeId.New(), AttendeeId.New(), AttendeeId.New() };
        var room = Room.Create(RoomId.New(), "2F Right", 3, isLocked: true, family);

        Assert.True(room.IsLocked);
        Assert.Equal(0, room.FreeSeats);
        Assert.Equal(family, room.Occupants);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_rejects_blank_name(string name)
    {
        Assert.Throws<DomainException>(() => Room.Create(RoomId.New(), name, 1));
    }

    [Fact]
    public void Create_rejects_zero_capacity()
    {
        Assert.Throws<DomainException>(() => Room.Create(RoomId.New(), "Room", 0));
    }

    [Fact]
    public void Create_rejects_seed_exceeding_capacity()
    {
        var seeds = new[] { AttendeeId.New(), AttendeeId.New(), AttendeeId.New() };
        Assert.Throws<DomainException>(() => Room.Create(RoomId.New(), "Tiny", capacity: 2, isLocked: false, seeds));
    }

    [Fact]
    public void AddOccupant_under_capacity_succeeds()
    {
        var room = Room.Create(RoomId.New(), "1F Queen", 3);
        room.ClearUncommittedEvents();

        room.AddOccupant(AttendeeId.New());

        Assert.Equal(2, room.FreeSeats);
        Assert.IsType<AttendeeJoinedRoom>(Assert.Single(room.UncommittedEvents));
    }

    [Fact]
    public void AddOccupant_beyond_capacity_throws()
    {
        var room = Room.Create(RoomId.New(), "Twin", 1);
        room.AddOccupant(AttendeeId.New());
        Assert.Throws<DomainException>(() => room.AddOccupant(AttendeeId.New()));
    }

    [Fact]
    public void AddOccupant_to_locked_room_throws()
    {
        var room = Room.Create(RoomId.New(), "2F Right", 3, isLocked: true);
        Assert.Throws<DomainException>(() => room.AddOccupant(AttendeeId.New()));
    }

    [Fact]
    public void RemoveOccupant_of_unknown_attendee_is_no_op()
    {
        var room = Room.Create(RoomId.New(), "Room", 3);
        room.ClearUncommittedEvents();

        room.RemoveOccupant(AttendeeId.New());

        Assert.Empty(room.UncommittedEvents);
    }

    [Fact]
    public void RemoveOccupant_from_locked_room_throws()
    {
        var attendee = AttendeeId.New();
        var room = Room.Create(RoomId.New(), "2F Right", 3, isLocked: true, new[] { attendee });
        Assert.Throws<DomainException>(() => room.RemoveOccupant(attendee));
    }
}
