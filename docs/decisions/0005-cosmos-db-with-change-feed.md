# 5. Cosmos DB as event store + change-feed-driven projections

Date: 2026-05-15

## Status

Accepted.

## Context

Given [ADR-0001](0001-event-sourcing.md) (event sourcing) we need:

1. An **event store** with append-only semantics, optimistic concurrency, and a partition strategy that scales horizontally if we ever need it to.
2. A **projection mechanism** that fans out events to denormalized read models, ideally without us implementing our own change-data-capture loop.

Candidate stacks:

- **EventStoreDB / Marten / Kurrent** — purpose-built event stores. Best-of-breed semantics, but adds a deployment unit and another piece of infrastructure to operate.
- **Postgres + a custom outbox + LISTEN/NOTIFY** — works, well-trodden, but we'd be writing change-feed plumbing ourselves.
- **Azure Cosmos DB** — append-only via document id, native change feed exposed as a SDK-level subscription (`ChangeFeedProcessor`), runs in Aspire via the Cosmos emulator container locally and provisions cleanly in Azure via `azd`.

Cosmos has two pricing modes: provisioned throughput (RU/s) and serverless. For a 4-day trip with ~10 attendees the serverless tier sits comfortably in the free tier ($0).

## Decision

Cosmos DB hosts both the write side and the read side:

**Write side** (`events` container, partition key `/aggregateId`):
- Each event is a document with id `"{AggregateType}|{AggregateId}|{Version}"`.
- Optimistic concurrency: two writers racing to version N both attempt the same id; one gets a 409 and we throw `ConcurrencyConflictException`.
- All events for an aggregate are colocated by partition, so loads are fast and the change feed delivers per-partition ordering (Cosmos guarantees ordered events per partition key).

**Read side** (one `view-*` container per view type):
- `view-attendees`, `view-carpools`, `view-rooms`, `view-parking-spots`, `view-projector-leases` — five containers.
- `ProjectionWorker` runs Cosmos's `ChangeFeedProcessor` against `events`, takes a per-partition lease in `view-projector-leases`, and for each event re-reads the aggregate (replays its full event stream) and upserts a flat view document.
- After every upsert, the projector publishes a `ViewUpdated(aggregateType, aggregateId)` integration event on the bus. The Web's `ViewUpdatedConsumer` translates that into a `LiveUpdates.NotifyViewUpdated` C# event, which subscribed Blazor circuits handle by calling `StateHasChanged()`.

The serializer was a deliberate choice too: Cosmos's default `CosmosClient` uses Newtonsoft.Json, which doesn't honor `[JsonPolymorphic]` from `System.Text.Json` — and we use that attribute on [`ParkingClaim`](../UBIQUITOUS_LANGUAGE.md#parkingclaim). We replaced it with a custom `SystemTextJsonCosmosSerializer` so wire format is consistent end-to-end (bus, Cosmos, browser).

## Consequences

**Good**

- Single piece of infrastructure for event store + projections. No outbox, no CDC tool, no extra deployment.
- Change feed gives us replay for free — to add a new view, write a projection and start a new lease at the beginning of time. (For our scale, "beginning of time" is hundreds of events.)
- Per-partition ordering is exactly what we need (events within an aggregate are ordered; cross-aggregate ordering doesn't matter).
- Serverless cost is effectively zero at our scale.
- Aspire's `AddAzureCosmosDB().RunAsEmulator()` makes local dev one container.

**Bad**

- The Cosmos emulator is slow to warm up (~30-60s). We added a retry loop in `CosmosProvisioner` and a `CosmosWarmupService` hosted service in Web to tolerate cold start.
- The emulator occasionally returns 404 from view containers before they're fully provisioned. `ViewStore.ListAll` catches `CosmosException(StatusCode == NotFound)` and returns empty — a small infra-leak into application code.
- Reading the full event stream on every projection (rather than projecting from just the new event) is wasteful at scale. At 10 attendees / 4 days it's a non-issue, and it keeps projections stateless and idempotent. If volume grew we'd switch to event-by-event projection with snapshot fallback.
- Multi-aggregate transactions are not possible. We use eventually-consistent reads (the projector catches up within ~100ms locally) and accept that two simultaneous events could land in views in any order. The UI tolerates this; the [optimistic UI](0008-optimistic-ui-updates.md) layer hides the latency.
