using BeachTrip.Application.Abstractions;
using BeachTrip.Application.Parking;
using BeachTrip.Domain;
using BeachTrip.Domain.Abstractions;
using BeachTrip.Domain.Attendees;
using BeachTrip.Domain.Carpools;
using MassTransit;

namespace BeachTrip.Application.Carpools;

public sealed class FormCarpoolConsumer : IConsumer<FormCarpool>
{
    private readonly IRepository<Carpool, CarpoolId> _carpools;
    private readonly IRepository<Attendee, AttendeeId> _attendees;

    public FormCarpoolConsumer(IRepository<Carpool, CarpoolId> carpools, IRepository<Attendee, AttendeeId> attendees)
    {
        _carpools = carpools;
        _attendees = attendees;
    }

    public async Task Consume(ConsumeContext<FormCarpool> ctx)
    {
        var driver = await _attendees.Get(ctx.Message.DriverId, ctx.CancellationToken)
            ?? throw new DomainException($"Driver {ctx.Message.DriverId} not found.");

        // When the driver joins a carpool, their solo parking claim (if any) is released.
        if (driver.HasCar)
        {
            await ctx.Publish(new SoloDriverReleasesParking(
                ParkingAllocationStateMachine.SagaId,
                ctx.Message.DriverId), ctx.CancellationToken);
        }

        var carpool = Carpool.Form(
            ctx.Message.CarpoolId,
            ctx.Message.DriverId,
            ctx.Message.CarCapacity,
            ctx.Message.Preference,
            ctx.Message.InitialPassengers);
        await _carpools.Save(carpool, ctx.CancellationToken);

        if (ctx.Message.Preference != ParkingPreference.None)
        {
            await ctx.Publish(new CarpoolWantsParking(
                ParkingAllocationStateMachine.SagaId,
                ctx.Message.CarpoolId,
                ctx.Message.Preference), ctx.CancellationToken);
        }
    }
}

public sealed class JoinCarpoolConsumer : IConsumer<JoinCarpool>
{
    private readonly IRepository<Carpool, CarpoolId> _carpools;
    public JoinCarpoolConsumer(IRepository<Carpool, CarpoolId> carpools) => _carpools = carpools;

    public async Task Consume(ConsumeContext<JoinCarpool> ctx)
    {
        var carpool = await _carpools.Get(ctx.Message.CarpoolId, ctx.CancellationToken)
            ?? throw new DomainException($"Carpool {ctx.Message.CarpoolId} not found.");
        carpool.AddMember(ctx.Message.AttendeeId);
        await _carpools.Save(carpool, ctx.CancellationToken);
    }
}

public sealed class LeaveCarpoolConsumer : IConsumer<LeaveCarpool>
{
    private readonly IRepository<Carpool, CarpoolId> _carpools;
    public LeaveCarpoolConsumer(IRepository<Carpool, CarpoolId> carpools) => _carpools = carpools;

    public async Task Consume(ConsumeContext<LeaveCarpool> ctx)
    {
        var carpool = await _carpools.Get(ctx.Message.CarpoolId, ctx.CancellationToken)
            ?? throw new DomainException($"Carpool {ctx.Message.CarpoolId} not found.");
        carpool.RemoveMember(ctx.Message.AttendeeId);
        await _carpools.Save(carpool, ctx.CancellationToken);

        if (!carpool.IsActive)
        {
            await ctx.Publish(new CarpoolReleasesParking(
                ParkingAllocationStateMachine.SagaId,
                ctx.Message.CarpoolId), ctx.CancellationToken);
        }
    }
}

public sealed class ChangeCarpoolPreferenceConsumer : IConsumer<ChangeCarpoolPreference>
{
    private readonly IRepository<Carpool, CarpoolId> _carpools;
    public ChangeCarpoolPreferenceConsumer(IRepository<Carpool, CarpoolId> carpools) => _carpools = carpools;

    public async Task Consume(ConsumeContext<ChangeCarpoolPreference> ctx)
    {
        var carpool = await _carpools.Get(ctx.Message.CarpoolId, ctx.CancellationToken)
            ?? throw new DomainException($"Carpool {ctx.Message.CarpoolId} not found.");
        carpool.ChangePreference(ctx.Message.Preference);
        await _carpools.Save(carpool, ctx.CancellationToken);

        if (ctx.Message.Preference == ParkingPreference.None)
        {
            await ctx.Publish(new CarpoolReleasesParking(
                ParkingAllocationStateMachine.SagaId,
                ctx.Message.CarpoolId), ctx.CancellationToken);
        }
        else
        {
            await ctx.Publish(new CarpoolWantsParking(
                ParkingAllocationStateMachine.SagaId,
                ctx.Message.CarpoolId,
                ctx.Message.Preference), ctx.CancellationToken);
        }
    }
}

public sealed class DisbandCarpoolConsumer : IConsumer<DisbandCarpool>
{
    private readonly IRepository<Carpool, CarpoolId> _carpools;
    public DisbandCarpoolConsumer(IRepository<Carpool, CarpoolId> carpools) => _carpools = carpools;

    public async Task Consume(ConsumeContext<DisbandCarpool> ctx)
    {
        var carpool = await _carpools.Get(ctx.Message.CarpoolId, ctx.CancellationToken)
            ?? throw new DomainException($"Carpool {ctx.Message.CarpoolId} not found.");
        carpool.Disband(ctx.Message.Reason);
        await _carpools.Save(carpool, ctx.CancellationToken);

        await ctx.Publish(new CarpoolReleasesParking(
            ParkingAllocationStateMachine.SagaId,
            ctx.Message.CarpoolId), ctx.CancellationToken);
    }
}
