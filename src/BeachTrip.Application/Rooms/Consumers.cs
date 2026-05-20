using BeachTrip.Application.Abstractions;
using BeachTrip.Domain;
using BeachTrip.Domain.Abstractions;
using BeachTrip.Domain.Rooms;
using MassTransit;

namespace BeachTrip.Application.Rooms;

public sealed class CreateRoomConsumer : IConsumer<CreateRoom>
{
    private readonly IRepository<Room, RoomId> _rooms;
    public CreateRoomConsumer(IRepository<Room, RoomId> rooms) => _rooms = rooms;

    public async Task Consume(ConsumeContext<CreateRoom> ctx)
    {
        var room = Room.Create(
            ctx.Message.RoomId,
            ctx.Message.Name,
            ctx.Message.Capacity,
            ctx.Message.IsLocked,
            ctx.Message.InitialOccupants);
        await _rooms.Save(room, ctx.CancellationToken);
    }
}

public sealed class AssignAttendeeToRoomConsumer : IConsumer<AssignAttendeeToRoom>
{
    private readonly IRepository<Room, RoomId> _rooms;
    public AssignAttendeeToRoomConsumer(IRepository<Room, RoomId> rooms) => _rooms = rooms;

    public async Task Consume(ConsumeContext<AssignAttendeeToRoom> ctx)
    {
        var room = await _rooms.Get(ctx.Message.RoomId, ctx.CancellationToken)
            ?? throw new DomainException($"Room {ctx.Message.RoomId} not found.");
        room.AddOccupant(ctx.Message.AttendeeId);
        await _rooms.Save(room, ctx.CancellationToken);
    }
}

public sealed class RemoveAttendeeFromRoomConsumer : IConsumer<RemoveAttendeeFromRoom>
{
    private readonly IRepository<Room, RoomId> _rooms;
    public RemoveAttendeeFromRoomConsumer(IRepository<Room, RoomId> rooms) => _rooms = rooms;

    public async Task Consume(ConsumeContext<RemoveAttendeeFromRoom> ctx)
    {
        var room = await _rooms.Get(ctx.Message.RoomId, ctx.CancellationToken)
            ?? throw new DomainException($"Room {ctx.Message.RoomId} not found.");
        room.RemoveOccupant(ctx.Message.AttendeeId);
        await _rooms.Save(room, ctx.CancellationToken);
    }
}
