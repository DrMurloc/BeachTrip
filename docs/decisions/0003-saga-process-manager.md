# 3. Saga (process manager) for parking allocation

Date: 2026-05-15

## Status

Accepted, but **dormant** as of 2026-05-17. Superseded in practice by [ADR-0006](0006-admin-override-replaces-saga.md) for the current product behavior; the saga remains fully implemented and tested.

## Context

Parking has a coordination problem that no single aggregate owns:

- 6 spots, ~10 attendees, some in carpools, some solo.
- Carpools should beat solo drivers for scarce spots.
- Preference (`Driveway` / `Street` / `None`) should be honored when possible.
- When a carpool forms, a previously-parked solo driver might need to be "bumped" — that's a cross-aggregate decision (`ParkingSpot` doesn't know about `Carpool`s).
- Allocation must be deterministic and serializable: two simultaneous carpool formations shouldn't race for the same spot.

Options:

1. **A service holding a `lock`** — `ParkingAllocationService` with a `SemaphoreSlim`. ~10 lines. Works on a single instance.
2. **A saga / process manager** — MassTransit state machine, persisted state, sequential per-instance processing by construction.
3. **Aggregate-level coordination via a "ParkingPool" aggregate** — possible, but turns parking into a single hot aggregate that every claim flows through.

For a domain this small, option 1 wins on simplicity. We picked option 2 anyway because:

- The over-engineering is the feature. This is a vehicle for tactical DDD and event-sourcing patterns, and a saga is a canonical pattern.
- Scaling to multiple Beach Episodes per instance is a 1-line change (correlate on `BeachEpisodeId` instead of a singleton GUID).
- `InMemoryTestHarness` makes saga integration tests fast and deterministic.
- The MassTransit state-machine DSL (`Initially`, `During`, `When`, `Then`, `TransitionTo`) reads almost like a spec.

## Decision

`ParkingAllocationStateMachine` in `BeachTrip.Application` is a single-instance MT saga with a well-known correlation GUID. It owns the parking inventory snapshot, a FIFO queue of claims (carpools then solos), and the current spot→claim assignments.

A pure-function allocator (`ParkingAllocator.Allocate`) takes inventory + queue and returns assignments. The saga diffs the new assignments against the previous state and publishes minimal delta events: `ParkingSpotAllocated`, `ParkingSpotReclaimed`, `SoloDriverBumped`, `ClaimUnmet`.

Bridge consumers in the Worker (`ApplyParkingSpotAllocatedConsumer`, `ApplyParkingSpotReclaimedConsumer`) translate saga decisions into aggregate operations on `ParkingSpot`, closing the loop.

Full mechanics in [SAGA.md](../SAGA.md).

## Consequences

**Good**

- Sequential per-instance processing eliminates a whole class of race bugs without explicit locks.
- The allocator is a pure function — 7 unit tests cover the core; 4 end-to-end tests cover the saga via `InMemoryTestHarness`.
- The state-machine DSL doubles as documentation. The transition graph in [SAGA.md](../SAGA.md) is reverse-engineered from the same code that ships.
- Adding a new claim type (e.g. visitor cars) is a queue-entry kind + a sort tier, not a refactor.

**Bad**

- The saga's persisted state lives in MT's in-memory saga repository in this build — it survives consumer restarts but not Worker restarts. For a 4-day trip that's fine; for prod we'd configure a real repository (Cosmos, EF, Redis).
- Sagas are heavier to reason about than a service-with-a-lock. New contributors need to read [SAGA.md](../SAGA.md) before changing it.
- See [ADR-0006](0006-admin-override-replaces-saga.md) — the saga is currently *dormant* by user request. The bridge consumers still work, but no `*WantsParking` events are emitted, so the queue stays empty and the allocator never runs in production. The saga remains tested and ready to re-enable in ~5 lines.
