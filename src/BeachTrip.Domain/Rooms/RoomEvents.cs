using BeachTrip.Domain.Abstractions;

namespace BeachTrip.Domain.Rooms;

public sealed record RoomCreated(
    RoomId RoomId,
    string Name,
    int Capacity,
    bool IsLocked,
    IReadOnlyList<AttendeeId> InitialOccupants) : DomainEvent;

public sealed record AttendeeJoinedRoom(RoomId RoomId, AttendeeId AttendeeId) : DomainEvent;

public sealed record AttendeeLeftRoom(RoomId RoomId, AttendeeId AttendeeId) : DomainEvent;
