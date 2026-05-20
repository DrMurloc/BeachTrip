using BeachTrip.Application.Abstractions;
using BeachTrip.Application.Parking;
using BeachTrip.Domain;
using BeachTrip.Domain.Abstractions;
using BeachTrip.Domain.Attendees;
using MassTransit;

namespace BeachTrip.Application.Attendees;

public sealed class RegisterAttendeeConsumer : IConsumer<RegisterAttendee>
{
    private readonly IRepository<Attendee, AttendeeId> _repo;
    public RegisterAttendeeConsumer(IRepository<Attendee, AttendeeId> repo) => _repo = repo;

    public async Task Consume(ConsumeContext<RegisterAttendee> ctx)
    {
        var attendee = Attendee.Register(ctx.Message.AttendeeId, ctx.Message.DisplayName);
        await _repo.Save(attendee, ctx.CancellationToken);
    }
}

public sealed class RenameAttendeeConsumer : IConsumer<RenameAttendee>
{
    private readonly IRepository<Attendee, AttendeeId> _repo;
    public RenameAttendeeConsumer(IRepository<Attendee, AttendeeId> repo) => _repo = repo;

    public async Task Consume(ConsumeContext<RenameAttendee> ctx)
    {
        var attendee = await _repo.Get(ctx.Message.AttendeeId, ctx.CancellationToken)
            ?? throw new DomainException($"Attendee {ctx.Message.AttendeeId} not found.");
        attendee.Rename(ctx.Message.DisplayName);
        await _repo.Save(attendee, ctx.CancellationToken);
    }
}

public sealed class DeclareAttendeeCarConsumer : IConsumer<DeclareAttendeeCar>
{
    private readonly IRepository<Attendee, AttendeeId> _repo;
    public DeclareAttendeeCarConsumer(IRepository<Attendee, AttendeeId> repo) => _repo = repo;

    public async Task Consume(ConsumeContext<DeclareAttendeeCar> ctx)
    {
        var attendee = await _repo.Get(ctx.Message.AttendeeId, ctx.CancellationToken)
            ?? throw new DomainException($"Attendee {ctx.Message.AttendeeId} not found.");
        attendee.DeclareCar(ctx.Message.Capacity, ctx.Message.Preference);
        await _repo.Save(attendee, ctx.CancellationToken);

        if (ctx.Message.Preference != ParkingPreference.None)
        {
            await ctx.Publish(new SoloDriverWantsParking(
                ParkingAllocationStateMachine.SagaId,
                ctx.Message.AttendeeId,
                ctx.Message.Preference), ctx.CancellationToken);
        }
    }
}

public sealed class UpdateAttendeeCarPreferenceConsumer : IConsumer<UpdateAttendeeCarPreference>
{
    private readonly IRepository<Attendee, AttendeeId> _repo;
    public UpdateAttendeeCarPreferenceConsumer(IRepository<Attendee, AttendeeId> repo) => _repo = repo;

    public async Task Consume(ConsumeContext<UpdateAttendeeCarPreference> ctx)
    {
        var attendee = await _repo.Get(ctx.Message.AttendeeId, ctx.CancellationToken)
            ?? throw new DomainException($"Attendee {ctx.Message.AttendeeId} not found.");
        attendee.UpdateCarPreference(ctx.Message.Preference);
        await _repo.Save(attendee, ctx.CancellationToken);

        if (ctx.Message.Preference == ParkingPreference.None)
        {
            await ctx.Publish(new SoloDriverReleasesParking(
                ParkingAllocationStateMachine.SagaId,
                ctx.Message.AttendeeId), ctx.CancellationToken);
        }
        else
        {
            await ctx.Publish(new SoloDriverWantsParking(
                ParkingAllocationStateMachine.SagaId,
                ctx.Message.AttendeeId,
                ctx.Message.Preference), ctx.CancellationToken);
        }
    }
}

public sealed class DropAttendeeCarConsumer : IConsumer<DropAttendeeCar>
{
    private readonly IRepository<Attendee, AttendeeId> _repo;
    public DropAttendeeCarConsumer(IRepository<Attendee, AttendeeId> repo) => _repo = repo;

    public async Task Consume(ConsumeContext<DropAttendeeCar> ctx)
    {
        var attendee = await _repo.Get(ctx.Message.AttendeeId, ctx.CancellationToken)
            ?? throw new DomainException($"Attendee {ctx.Message.AttendeeId} not found.");
        attendee.DropCar();
        await _repo.Save(attendee, ctx.CancellationToken);

        await ctx.Publish(new SoloDriverReleasesParking(
            ParkingAllocationStateMachine.SagaId,
            ctx.Message.AttendeeId), ctx.CancellationToken);
    }
}
