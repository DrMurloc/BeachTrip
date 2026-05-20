using BeachTrip.Domain;
using BeachTrip.Domain.Attendees;
using BeachTrip.Domain.Parking;
using MassTransit;

namespace BeachTrip.Application.Parking;

public sealed class ParkingAllocationStateMachine : MassTransitStateMachine<ParkingAllocationState>
{
    public static readonly Guid SagaId = Guid.Parse("a4e9c8d2-bbbb-4abc-9def-aaaaaaaaaaaa");

    public State Active { get; private set; } = null!;

    public Event<SeedParkingInventory> SeedInventory { get; private set; } = null!;
    public Event<CarpoolWantsParking> CarpoolWantsParking { get; private set; } = null!;
    public Event<CarpoolReleasesParking> CarpoolReleasesParking { get; private set; } = null!;
    public Event<SoloDriverWantsParking> SoloDriverWantsParking { get; private set; } = null!;
    public Event<SoloDriverReleasesParking> SoloDriverReleasesParking { get; private set; } = null!;
    public Event<SpotTakenOutOfPool> SpotTakenOutOfPool { get; private set; } = null!;
    public Event<SpotReturnedToPool> SpotReturnedToPool { get; private set; } = null!;

    public ParkingAllocationStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => SeedInventory, x =>
        {
            x.CorrelateById(m => m.Message.CorrelationId);
            x.SelectId(m => m.Message.CorrelationId);
            x.InsertOnInitial = true;
        });
        Event(() => CarpoolWantsParking, x =>
        {
            x.CorrelateById(m => m.Message.CorrelationId);
            x.SelectId(m => m.Message.CorrelationId);
            x.InsertOnInitial = true;
        });
        Event(() => CarpoolReleasesParking, x =>
        {
            x.CorrelateById(m => m.Message.CorrelationId);
            x.SelectId(m => m.Message.CorrelationId);
            x.InsertOnInitial = true;
        });
        Event(() => SoloDriverWantsParking, x =>
        {
            x.CorrelateById(m => m.Message.CorrelationId);
            x.SelectId(m => m.Message.CorrelationId);
            x.InsertOnInitial = true;
        });
        Event(() => SoloDriverReleasesParking, x =>
        {
            x.CorrelateById(m => m.Message.CorrelationId);
            x.SelectId(m => m.Message.CorrelationId);
            x.InsertOnInitial = true;
        });
        Event(() => SpotTakenOutOfPool, x =>
        {
            x.CorrelateById(m => m.Message.CorrelationId);
            x.SelectId(m => m.Message.CorrelationId);
            x.InsertOnInitial = true;
        });
        Event(() => SpotReturnedToPool, x =>
        {
            x.CorrelateById(m => m.Message.CorrelationId);
            x.SelectId(m => m.Message.CorrelationId);
            x.InsertOnInitial = true;
        });

        Initially(
            When(SeedInventory)
                .Then(ctx => Seed(ctx.Saga, ctx.Message.Spots))
                .TransitionTo(Active),
            When(CarpoolWantsParking)
                .ThenAsync(ctx => EnqueueAndReallocate(ctx, SagaClaim.ForCarpool(ctx.Message.CarpoolId, ctx.Message.Preference)))
                .TransitionTo(Active),
            When(SoloDriverWantsParking)
                .ThenAsync(ctx => EnqueueAndReallocate(ctx, SagaClaim.ForSolo(ctx.Message.AttendeeId, ctx.Message.Preference)))
                .TransitionTo(Active)
        );

        During(Active,
            When(SeedInventory)
                .Then(ctx => Seed(ctx.Saga, ctx.Message.Spots)),
            When(CarpoolWantsParking)
                .ThenAsync(ctx => EnqueueAndReallocate(ctx, SagaClaim.ForCarpool(ctx.Message.CarpoolId, ctx.Message.Preference))),
            When(CarpoolReleasesParking)
                .ThenAsync(ctx => DequeueAndReallocate(ctx, ClaimKind.Carpool, ctx.Message.CarpoolId.Value)),
            When(SoloDriverWantsParking)
                .ThenAsync(ctx => EnqueueAndReallocate(ctx, SagaClaim.ForSolo(ctx.Message.AttendeeId, ctx.Message.Preference))),
            When(SoloDriverReleasesParking)
                .ThenAsync(ctx => DequeueAndReallocate(ctx, ClaimKind.Solo, ctx.Message.AttendeeId.Value)),
            When(SpotTakenOutOfPool)
                .ThenAsync(ctx => SetSpotLockAndReallocate(ctx, ctx.Message.SpotId, locked: true)),
            When(SpotReturnedToPool)
                .ThenAsync(ctx => SetSpotLockAndReallocate(ctx, ctx.Message.SpotId, locked: false))
        );
    }

    private static Task SetSpotLockAndReallocate<T>(BehaviorContext<ParkingAllocationState, T> ctx, ParkingSpotId spotId, bool locked) where T : class
    {
        var idx = ctx.Saga.Spots.FindIndex(s => s.SpotId == spotId);
        if (idx < 0) return Task.CompletedTask;
        ctx.Saga.Spots[idx] = ctx.Saga.Spots[idx] with { IsLocked = locked };
        return Reallocate(ctx);
    }

    private static void Seed(ParkingAllocationState saga, IReadOnlyList<SpotSeed> seeds)
    {
        saga.Spots.Clear();
        foreach (var seed in seeds)
            saga.Spots.Add(new SagaSpot(seed.Id, seed.Name, seed.Type, seed.IsLocked, seed.LockedClaim));
    }

    private static Task EnqueueAndReallocate<T>(BehaviorContext<ParkingAllocationState, T> ctx, SagaClaim claim) where T : class
    {
        // Replace prior claim from same claimant (handles preference changes).
        ctx.Saga.Queue.RemoveAll(c => c.Kind == claim.Kind && c.ClaimantId == claim.ClaimantId);
        ctx.Saga.Queue.Add(claim);
        return Reallocate(ctx);
    }

    private static Task DequeueAndReallocate<T>(BehaviorContext<ParkingAllocationState, T> ctx, ClaimKind kind, Guid claimantId) where T : class
    {
        ctx.Saga.Queue.RemoveAll(c => c.Kind == kind && c.ClaimantId == claimantId);
        return Reallocate(ctx);
    }

    private static async Task Reallocate<T>(BehaviorContext<ParkingAllocationState, T> ctx) where T : class
    {
        var outcome = ParkingAllocator.Allocate(ctx.Saga.Spots, ctx.Saga.Queue);
        var previous = ctx.Saga.Assignments;
        var next = outcome.Assignments;

        var prevBySpot = previous.ToDictionary(a => a.SpotId);
        var nextBySpot = next.ToDictionary(a => a.SpotId);

        // Spots that became free or changed claimant.
        foreach (var prev in previous)
        {
            if (!nextBySpot.TryGetValue(prev.SpotId, out var now) || now.Claim != prev.Claim)
                await ctx.Publish(new ParkingSpotReclaimed(prev.SpotId));
        }

        // Spots that gained a new claim (or changed).
        foreach (var now in next)
        {
            if (!prevBySpot.TryGetValue(now.SpotId, out var prev) || prev.Claim != now.Claim)
            {
                ParkingClaim claim = now.Claim.Kind == ClaimKind.Carpool
                    ? new ParkingClaim.Carpool(new CarpoolId(now.Claim.ClaimantId))
                    : new ParkingClaim.Solo(new AttendeeId(now.Claim.ClaimantId));
                await ctx.Publish(new ParkingSpotAllocated(now.SpotId, claim));
            }
        }

        // Solo drivers that lost their spot (bumped).
        var nextClaimSet = new HashSet<(ClaimKind kind, Guid id)>(next.Select(a => (a.Claim.Kind, a.Claim.ClaimantId)));
        foreach (var prev in previous)
        {
            if (prev.Claim.Kind != ClaimKind.Solo) continue;
            if (!nextClaimSet.Contains((ClaimKind.Solo, prev.Claim.ClaimantId)))
                await ctx.Publish(new SoloDriverBumped(new AttendeeId(prev.Claim.ClaimantId)));
        }

        // Claims still in queue but unassigned.
        var assignedSet = new HashSet<(ClaimKind kind, Guid id)>(next.Select(a => (a.Claim.Kind, a.Claim.ClaimantId)));
        foreach (var claim in ctx.Saga.Queue)
        {
            if (!assignedSet.Contains((claim.Kind, claim.ClaimantId)))
                await ctx.Publish(new ClaimUnmet(claim.Kind, claim.ClaimantId, claim.Preference));
        }

        ctx.Saga.Assignments = next.ToList();
    }
}
