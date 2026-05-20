# 1. Event-source all aggregates

Date: 2026-05-15

## Status

Accepted.

## Context

The domain has four aggregates ([Attendee](../UBIQUITOUS_LANGUAGE.md#attendee), [Carpool](../UBIQUITOUS_LANGUAGE.md#carpool), [Room](../UBIQUITOUS_LANGUAGE.md#room), [ParkingSpot](../UBIQUITOUS_LANGUAGE.md#parkingspot)) that change state through small, well-named domain operations: `Register`, `JoinRoom`, `FormCarpool`, `OverrideTo`, etc. Each operation is conceptually a *fact* — something that happened — not a row update.

We considered three persistence styles:

1. **CRUD with EF Core**: write a row per aggregate, mutate in place. Familiar; fast to ship.
2. **Snapshots + audit log**: persist current state plus an append-only log of changes for audit.
3. **Event sourcing**: events are the source of truth; current state is a fold over events.

The overarching goal of this project is "deliberately over-engineered for ~10 people across 4 days." That tilts toward the option that maximizes traceability, replay-ability, and clean separation between writes and reads — and that gives us a real story for the projection side too.

Cosmos DB has native append-only semantics via document-id collision and a change feed that emits every write in insertion order per partition. That makes it a natural event store for free, without needing a separate KurrentDB / Marten / EventStore deployment.

## Decision

All four aggregates are event-sourced. Every state change is expressed as an immutable [domain event](../UBIQUITOUS_LANGUAGE.md#domain-event) (`AttendeeRegistered`, `AttendeeJoinedRoom`, `CarpoolFormed`, `ParkingSpotManuallyAssigned`, etc.) persisted in the Cosmos `events` container.

- `AggregateRoot<TId>` exposes `Raise(event)` to append a pending event and `Apply(event)` for each derived aggregate to fold it into local state.
- `CosmosEventRepository` writes events with document id `"{AggregateType}|{AggregateId}|{Version}"`. Two writers racing to version N both attempt the same id; one gets a 409 and surfaces as `ConcurrencyConflictException` — optimistic concurrency for free, no `ETag` plumbing.
- Loads replay all events for an aggregate id (partition key `/aggregateId`).
- The change feed processor in `ProjectionWorker` consumes events and rebuilds flat read-model documents in dedicated `view-*` containers.

See [ARCHITECTURE.md](../../ARCHITECTURE.md) for the broader topology and [MESSAGE_CONTRACTS.md](../MESSAGE_CONTRACTS.md) for the full event catalog.

## Consequences

**Good**

- Full audit trail by construction. Every state change is on disk forever with `OccurredAt` and the operation that caused it.
- Replay-driven projections. A new view shape can be added by writing a new projection and replaying from the change feed's beginning — no migration script.
- Optimistic concurrency without explicit version columns: id collision does the work.
- Tests are clean: aggregate behavior is `Apply(givenEvents); act(); assert(pendingEvents)`. No mocks, no fakes, no DB.
- Forces small, named operations on aggregates instead of bag-of-setters.

**Bad**

- Aggregate loads cost N reads (one per event). With ~10 attendees over 4 days, N is small; at higher scale we'd need snapshots.
- Schema evolution requires care: old events live forever and must keep deserializing. We've avoided the problem so far by not shipping the app yet.
- No ad-hoc query on aggregate state — must go through a [view](../UBIQUITOUS_LANGUAGE.md#view).
- Newcomers used to CRUD need to internalize "current state is a fold." [UBIQUITOUS_LANGUAGE.md](../UBIQUITOUS_LANGUAGE.md) and [EVENT_STORM.md](../EVENT_STORM.md) exist partly to ease that.
