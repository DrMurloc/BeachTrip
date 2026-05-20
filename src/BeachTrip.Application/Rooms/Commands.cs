using BeachTrip.Domain;

namespace BeachTrip.Application.Rooms;

public sealed record CreateRoom(RoomId RoomId, string Name, int Capacity, bool IsLocked = false, IReadOnlyList<AttendeeId>? InitialOccupants = null);

public sealed record AssignAttendeeToRoom(RoomId RoomId, AttendeeId AttendeeId);

public sealed record RemoveAttendeeFromRoom(RoomId RoomId, AttendeeId AttendeeId);
