# The Parking Allocation Saga

Single-instance MassTransit state machine that owns the parking inventory and decides who parks where. Currently **dormant** by user request — the command consumers no longer feed it input events, so its queue stays empty and it makes no allocations. Admin-driven overrides handle parking instead.

The saga remains fully wired, tested, and consistent: admin overrides flow through `SpotTakenOutOfPool` / `SpotReturnedToPool` so the saga's view of the parking pool stays accurate. Reinstating auto-allocation is a 5-line change to the command consumers.

## Why a saga and not a service

A saga gives us:
1. **Persisted state across messages** (`ParkingAllocationState` saved to MT's saga repository)
2. **Sequential per-instance processing** (no race conditions on the spot inventory)
3. **Declarative state-transition syntax** (the MT state machine DSL)
4. **Free integration testing** (`InMemoryTestHarness`)

The alternative — a service holding a `lock` — would have been ~10 lines for this scale. We picked the saga because it scales to multiple Beach Episodes per instance (correlate on a `BeachEpisodeId` instead of the singleton GUID) and because the over-engineering is the feature.

## States and events

```
                          ┌─────────────┐
                          │  Initial    │
                          └──────┬──────┘
                                 │
              first input event (any of):
              SeedParkingInventory,
              CarpoolWantsParking,
              SoloDriverWantsParking
                                 │
                                 ▼
                          ┌─────────────┐
                          │   Active    │◄────────────────┐
                          └──────┬──────┘                 │
                                 │                        │
                          handles all events:             │
                          - SeedParkingInventory          │ (re-runs Reallocate
                          - CarpoolWantsParking           │  after each one)
                          - CarpoolReleasesParking        │
                          - SoloDriverWantsParking        │
                          - SoloDriverReleasesParking     │
                          - SpotTakenOutOfPool            │
                          - SpotReturnedToPool ───────────┘
```

There's only one terminal state (Active) — the saga never completes. Its singleton instance lives forever (or until the Worker process recycles).

## State payload

```csharp
class ParkingAllocationState
{
    Guid   CorrelationId;     // well-known: a4e9c8d2-bbbb-4abc-9def-aaaaaaaaaaaa
    string CurrentState;      // Active

    List<SagaSpot>           Spots;        // inventory snapshot
    List<SagaClaim>          Queue;        // waiting claims, first-come-first-served per tier
    List<SagaSpotAssignment> Assignments;  // current spot → claim mapping
}
```

`SagaSpot` mirrors the ParkingSpot aggregate's IsLocked flag — when admin overrides a spot, `SpotTakenOutOfPool` flips it true in the saga's view so the allocator stops considering it.

## Inputs (saga consumes)

| Message | Effect |
|---|---|
| `SeedParkingInventory(spots[])` | Replaces saga's inventory; transitions to Active if Initial. |
| `CarpoolWantsParking(carpoolId, preference)` | Adds/updates a Carpool claim in the queue + Reallocate. *Currently never emitted.* |
| `CarpoolReleasesParking(carpoolId)` | Removes the Carpool's queue entry + Reallocate. *Currently never emitted.* |
| `SoloDriverWantsParking(attendeeId, preference)` | Adds/updates a Solo claim in the queue + Reallocate. *Currently never emitted.* |
| `SoloDriverReleasesParking(attendeeId)` | Removes the Solo's queue entry + Reallocate. *Currently never emitted.* |
| `SpotTakenOutOfPool(spotId)` | Marks SagaSpot[spotId].IsLocked = true + Reallocate. (Emitted by admin assignment consumer.) |
| `SpotReturnedToPool(spotId)` | Marks SagaSpot[spotId].IsLocked = false + Reallocate. (Emitted by admin release consumer.) |

## Outputs (saga publishes after each Reallocate)

| Message | When |
|---|---|
| `ParkingSpotAllocated(spotId, claim)` | A spot's claim changed (new claim, or different claimant) |
| `ParkingSpotReclaimed(spotId)` | A spot's prior claim is gone |
| `SoloDriverBumped(attendeeId)` | A solo driver who had a spot no longer does |
| `ClaimUnmet(kind, claimantId, preference)` | A claim is still in the queue but unallocated |

The Web's `SoloDriverBumpedConsumer` filters by current user and toasts; the bridge consumers (`ApplyParkingSpotAllocated` / `ApplyParkingSpotReclaimed`) translate spot changes into aggregate state.

## The allocator algorithm

`ParkingAllocator.Allocate(spots, queue)` is a pure function. Given the inventory and the queue, returns a list of assignments. No side effects, no I/O, ~30 lines.

```
sort queue by tier:
    1. all Carpool claims (in queue order — first-come-first-served)
    2. all Solo claims (in queue order)

remaining = { spotId : spot for spot in spots if not spot.IsLocked }

for claim in (carpools then solos):
    preferred_type = match claim.preference:
        Driveway → Driveway
        Street → Street
        None → any

    spot = first in `remaining` matching preferred_type
    if spot is null and preferred_type is set:
        spot = first in `remaining` of any type   (fallback)
    if spot is null:
        skip this claim (will be reported as ClaimUnmet)
    else:
        assignments.append((spot, claim))
        remove spot from remaining

return assignments
```

Properties this guarantees (verified by `ParkingAllocatorTests`):
- Carpools always beat solos for scarce spots
- Within a tier, queue order breaks ties (oldest wins)
- Preference is honored when possible, falls back to any free spot
- Locked spots are never allocated
- A spot held by a claim that's still in the queue stays with that claim across reallocations (no churn)

## The reallocator (`Reallocate` in the state machine)

After every queue/inventory change, the saga:

1. Calls `ParkingAllocator.Allocate(state.Spots, state.Queue)` → `outcome.Assignments`
2. Diffs against `state.Assignments`:
   - Spots that gained or changed a claim → publish `ParkingSpotAllocated`
   - Spots that lost their claim → publish `ParkingSpotReclaimed`
   - Solo claims that previously had a spot but no longer do → publish `SoloDriverBumped`
   - Claims still in the queue but unassigned → publish `ClaimUnmet`
3. Replaces `state.Assignments` with the new outcome

This diff-then-publish pattern means the bus carries minimal change events, not full snapshots. UI sees only deltas.

## Idempotency

Every input message replaces or removes by `(kind, claimantId)`. Receiving the same `CarpoolWantsParking(c1, Driveway)` twice produces one queue entry, one Reallocate, no duplicate output messages. The MT message dedup + saga's queue-by-identity makes the saga safe under at-least-once delivery.

## Concurrency

Single-instance, single correlation id → MassTransit serializes message processing per saga instance. No internal locking needed.

## Reactivating auto-allocation

If you want the saga to drive parking again instead of admin-only:

1. **In `DeclareAttendeeCarConsumer`** — when `Preference != None`:
   ```csharp
   await ctx.Publish(new SoloDriverWantsParking(
       ParkingAllocationStateMachine.SagaId,
       ctx.Message.AttendeeId,
       ctx.Message.Preference));
   ```
2. **In `FormCarpoolConsumer`** — when `Preference != None`, similar:
   ```csharp
   await ctx.Publish(new CarpoolWantsParking(...));
   ```
3. **In the corresponding "release" consumers** — `DropAttendeeCarConsumer`, `DisbandCarpoolConsumer`, `LeaveCarpoolConsumer` (when carpool dies), `ChangeCarpoolPreferenceConsumer` (when preference goes to None) — publish the matching `*ReleasesParking` event.
4. **In the lobby UI** — restore the parking preference inputs that were stripped during the admin-override pivot.

The bridge consumers (`ApplyParkingSpotAllocated`, `ApplyParkingSpotReclaimed`) are already wired and will turn saga decisions into aggregate state changes. The admin override path keeps working alongside; admin-locked spots are skipped by the allocator, so the two coexist cleanly.

## Tests

`BeachTrip.Application.Tests.ParkingAllocatorTests` — 7 pure-function tests covering empty queue, single-carpool allocation, carpool-beats-solo, preference matching, preference fallback, locked-spot exclusion, multi-carpool/solo tiering.

`BeachTrip.Application.Tests.ParkingAllocationSagaTests` — 4 end-to-end tests via MT's `InMemoryTestHarness`:
- Single carpool with seeded inventory gets a spot
- Carpool bumps solo from only spot (verifies `SoloDriverBumped`)
- Solo recovers spot when carpool disbands
- Multiple carpools prefer matching spot types

All 11 still passing; flipping the saga back on doesn't require changing the tests.
