using BeachTrip.Application.Abstractions;
using BeachTrip.Domain;
using BeachTrip.Domain.Parking;
using MassTransit;

namespace BeachTrip.Application.Parking;

// Bridges saga decisions back to the ParkingSpot aggregate. The saga publishes
// ParkingSpotAllocated / ParkingSpotReclaimed when its allocation algorithm picks
// new winners; these consumers translate those decisions into aggregate state so
// the ParkingSpot's events land in Cosmos and the view projection picks them up.

public sealed class ApplyParkingSpotAllocatedConsumer : IConsumer<ParkingSpotAllocated>
{
    private readonly IRepository<ParkingSpot, ParkingSpotId> _repo;
    public ApplyParkingSpotAllocatedConsumer(IRepository<ParkingSpot, ParkingSpotId> repo) => _repo = repo;

    public async Task Consume(ConsumeContext<ParkingSpotAllocated> ctx)
    {
        var spot = await _repo.Get(ctx.Message.SpotId, ctx.CancellationToken);
        if (spot is null || spot.IsLocked) return;

        switch (ctx.Message.Claim)
        {
            case ParkingClaim.Carpool c:
                spot.AssignToCarpool(c.CarpoolId);
                break;
            case ParkingClaim.Solo s:
                spot.AssignToSoloDriver(s.AttendeeId);
                break;
        }
        await _repo.Save(spot, ctx.CancellationToken);
    }
}

public sealed class ApplyParkingSpotReclaimedConsumer : IConsumer<ParkingSpotReclaimed>
{
    private readonly IRepository<ParkingSpot, ParkingSpotId> _repo;
    public ApplyParkingSpotReclaimedConsumer(IRepository<ParkingSpot, ParkingSpotId> repo) => _repo = repo;

    public async Task Consume(ConsumeContext<ParkingSpotReclaimed> ctx)
    {
        var spot = await _repo.Get(ctx.Message.SpotId, ctx.CancellationToken);
        if (spot is null || spot.IsLocked) return;
        spot.Release();
        await _repo.Save(spot, ctx.CancellationToken);
    }
}

public sealed class ManuallyAssignSpotConsumer : IConsumer<ManuallyAssignSpot>
{
    private readonly IRepository<ParkingSpot, ParkingSpotId> _repo;
    public ManuallyAssignSpotConsumer(IRepository<ParkingSpot, ParkingSpotId> repo) => _repo = repo;

    public async Task Consume(ConsumeContext<ManuallyAssignSpot> ctx)
    {
        var spot = await _repo.Get(ctx.Message.SpotId, ctx.CancellationToken)
            ?? throw new Domain.Abstractions.DomainException($"Parking spot {ctx.Message.SpotId} not found.");
        spot.OverrideTo(ctx.Message.Claim);
        await _repo.Save(spot, ctx.CancellationToken);

        // Tell the saga this spot is no longer in the allocation pool.
        await ctx.Publish(new SpotTakenOutOfPool(
            ParkingAllocationStateMachine.SagaId,
            ctx.Message.SpotId), ctx.CancellationToken);
    }
}

public sealed class RemoveSpotOverrideConsumer : IConsumer<RemoveSpotOverride>
{
    private readonly IRepository<ParkingSpot, ParkingSpotId> _repo;
    public RemoveSpotOverrideConsumer(IRepository<ParkingSpot, ParkingSpotId> repo) => _repo = repo;

    public async Task Consume(ConsumeContext<RemoveSpotOverride> ctx)
    {
        var spot = await _repo.Get(ctx.Message.SpotId, ctx.CancellationToken)
            ?? throw new Domain.Abstractions.DomainException($"Parking spot {ctx.Message.SpotId} not found.");
        spot.RemoveOverride();
        await _repo.Save(spot, ctx.CancellationToken);

        await ctx.Publish(new SpotReturnedToPool(
            ParkingAllocationStateMachine.SagaId,
            ctx.Message.SpotId), ctx.CancellationToken);
    }
}
