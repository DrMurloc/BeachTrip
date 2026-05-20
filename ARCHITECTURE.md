# Architecture

A 4-day beach trip planning app, structured like it'll outlive the heat death of the universe.

## Layer cake

```
┌──────────────────────────────────────────────────────────┐
│  Web (Blazor Server)             Worker (BackgroundSvc)  │
│  - MudBlazor UI                  - 13 command consumers  │
│  - LiveUpdates broadcaster       - Saga state machine    │
│  - 2 UI consumers (ViewUpdated,  - 2 bridge consumers    │
│    SoloDriverBumped)               (saga → aggregate)    │
│  - ViewStore (Cosmos reads)      - CatalogSeeder         │
│  - CosmosWarmupService           - ProjectionWorker      │
└────────┬─────────────────────────────────────┬───────────┘
         │           BeachTrip.Application     │
         │  ───────────────────────────────────│
         │   commands · saga · consumers       │
         │   IRepository<TAgg, TId> contract   │
         ▼                                     ▼
┌─────────────────────────┐    ┌───────────────────────────┐
│ BeachTrip.Domain        │    │ BeachTrip.Infrastructure  │
│ (zero external deps)    │    │ - CosmosEventRepository   │
│ - AggregateRoot<TId>    │    │ - CosmosClient + STJ      │
│ - Domain events         │    │ - Projection workers      │
│ - Value objects         │    │ - DI extensions           │
│ - Strongly-typed IDs    │    │ - MassTransit setup       │
└─────────────────────────┘    └───────────────────────────┘
```

**Dependency direction is strict**: Web/Worker → Application → Domain. Infrastructure → Application + Domain. Domain references nothing.

`BeachTrip.ServiceDefaults` is a transverse module both Web and Worker reference for shared Aspire wiring (OpenTelemetry, health checks, service discovery).

## Bounded contexts (sort of)

The domain is small enough that everything lives in one assembly, but the aggregates are organized by sub-domain folder so the contexts are visually obvious:

- `Attendees/` — who's coming, what handle, do they have a car
- `Carpools/` — groupings of attendees driving up together
- `Rooms/` — physical sleeping spaces with capacity
- `Parking/` — physical parking spots + claim semantics

Each folder is self-contained: aggregate root, events, value objects, no cross-folder references except through ID types.

---

## Anatomy of a click

When a user clicks "Join" on a room card:

```
 1. Lobby.razor calls JoinRoom(roomId)
 2. Optimistic update: mutate _rooms locally + StateHasChanged()
 3. IBus.Publish(new AssignAttendeeToRoom(roomId, attendeeId))
                ↓
       RabbitMQ (Aspire-hosted)
                ↓
 4. Worker's AssignAttendeeToRoomConsumer picks up the message
 5. Loads Room aggregate via CosmosEventRepository.Get(id)
       — reads all events for that aggregate's partition
       — replays them through Room.Apply(event) to reconstruct state
 6. Calls room.AddOccupant(attendeeId)
       — validates the invariant (capacity, not locked, etc.)
       — Raises AttendeeJoinedRoom event into the aggregate's
         uncommitted list
 7. Saves via repo: writes the new event as a Cosmos document
       — id = "Room|<roomId>|<version>"
       — optimistic concurrency via id-collision (409)
                ↓
       Cosmos change feed
                ↓
 8. ProjectionWorker (hosted service in Worker) picks up the new doc
 9. Re-loads the Room aggregate, projects to RoomView (a flat DTO)
10. Upserts RoomView into the "view-rooms" Cosmos container
11. Publishes ViewUpdated("Room", roomId) over RabbitMQ
                ↓
       RabbitMQ → Web's ViewUpdatedConsumer
                ↓
12. Calls into LiveUpdates singleton, which raises a C# event
13. Every open Lobby component has subscribed; OnViewChanged fires
14. await InvokeAsync(() => { Refresh(); StateHasChanged(); })
15. SignalR pushes UI deltas to every connected browser

Elapsed: ~300-500ms end-to-end on the emulator. The optimistic
update from step 2 makes the originating user see the change
in <50ms; the network round-trip reconciles when it lands.
```

## The read path

For initial page loads and post-update reconciles, the Lobby's `ViewStore` queries the Cosmos view containers directly via `SELECT * FROM c`:

- `view-attendees` (PK `/id`)
- `view-carpools` (PK `/id`)
- `view-rooms` (PK `/id`)
- `view-parking-spots` (PK `/id`)

These are flat denormalized documents — a `RoomView` carries its `Capacity`, `Occupants[]`, `IsLocked`, `FreeSeats`. No joins needed.

`ViewStore.ListAll<T>` tolerates `CosmosException(NotFound)` and returns an empty list — this lets Web boot before Worker has provisioned containers without crashing the lobby.

## The saga

`ParkingAllocationStateMachine` is a single-instance MassTransit state machine (correlation id is a well-known GUID). Originally drove automatic spot allocation — carpools beat solos, preferences honored, bumps published when a higher-priority claim displaced a solo.

The saga still exists, still has all its tests passing, and is **currently dormant** because admin-driven manual assignment (DrMurloc clicks a spot, picks who gets it) replaced the auto-allocation user experience. The saga learns of admin overrides via `SpotTakenOutOfPool` / `SpotReturnedToPool` messages so its inventory view stays consistent.

If you ever want auto-allocation back, set the saga's input events firing again from the command consumers and it'll just work.

See [docs/SAGA.md](docs/SAGA.md) for the state diagram and allocator algorithm.

## Distributed topology

Local (via Aspire AppHost):

```
┌─────────────────────────────────────────────────────────┐
│  BeachTrip.AppHost  →  Aspire dashboard @ :17188        │
│  ├─ Web      (Blazor Server, browser-facing HTTPS)      │
│  ├─ Worker   (BackgroundService host)                   │
│  ├─ rabbitmq (image: rabbitmq:4.2-management)           │
│  └─ cosmos   (image: cosmosdb/linux/azure-cosmos-emulator)│
└─────────────────────────────────────────────────────────┘
```

`azd up`'d to Azure:

```
┌─────────────────────────────────────────────────────────┐
│  Resource Group                                          │
│  ├─ Container Apps Environment                          │
│  │  ├─ web    (ACA, external ingress, scale 1-N)        │
│  │  ├─ worker (ACA, internal, scale 1)                  │
│  │  └─ rabbitmq (ACA, internal, scale 1)                │
│  ├─ Azure Cosmos DB account (NoSQL)                     │
│  │  └─ beachtrip database with 8 containers             │
│  ├─ Azure Container Registry                            │
│  └─ Log Analytics workspace + Application Insights      │
└─────────────────────────────────────────────────────────┘
```

The container/service mapping in `AppHost.cs` is intentionally identical between modes — Aspire transparently swaps emulators for real Azure resources at publish time.

## Cosmos schema

One database, eight containers:

| Container | Partition key | What's in it |
|---|---|---|
| `events` | `/aggregateId` | Append-only event stream, one doc per `{aggregate, version}` |
| `sagas` | `/id` | (Allocated but unused — saga state is in-memory) |
| `projection-leases` | `/id` | Cosmos change-feed lease tracking |
| `view-attendees` | `/id` | Read-model projection per Attendee |
| `view-carpools` | `/id` | Read-model projection per Carpool |
| `view-rooms` | `/id` | Read-model projection per Room |
| `view-parking-spots` | `/id` | Read-model projection per ParkingSpot |
| `view-parking-allocation` | `/id` | (Reserved for the saga's published view; currently unused) |

Event documents:

```json
{
  "id": "Room|a1000000-0000-0000-0000-000000000004|3",
  "aggregateType": "Room",
  "aggregateId": "a1000000-0000-0000-0000-000000000004",
  "eventType": "AttendeeJoinedRoom",
  "version": 3,
  "occurredAt": "2026-05-20T14:32:11.421Z",
  "data": { "roomId": "...", "attendeeId": "..." }
}
```

The `data` field uses System.Text.Json end-to-end (a `SystemTextJsonCosmosSerializer` overrides the SDK's Newtonsoft default), with a `StronglyTypedIdConverterFactory` so the four record-struct IDs serialize as bare GUIDs instead of `{ "value": "guid" }`. `ParkingClaim` is `[JsonPolymorphic]` with a `$kind` discriminator.

## Live updates

```
ProjectionWorker (Worker)
    │
    │ after each view upsert:
    │   Publish(new ViewUpdated(aggregateType, aggregateId))
    ▼
RabbitMQ
    │
    ▼
ViewUpdatedConsumer (Web, one per replica)
    │
    │ Live.NotifyViewUpdated(message)
    ▼
LiveUpdates singleton (Web)  — C# event
    │
    │ ViewChanged event fires on every subscriber
    ▼
Lobby + MainLayout components
    │
    │ InvokeAsync(() => { Refresh(); StateHasChanged(); })
    ▼
SignalR pushes diff to every connected circuit
```

`SoloDriverBumped` follows the same path but routes to a snackbar instead of a refresh — only the affected attendee's circuit shows the toast.

## Identity (or lack thereof)

There is no authentication. `IdentityService` is a thin wrapper around `ProtectedSessionStorage` that holds a self-claimed `{ AttendeeId, DisplayName }` per circuit. Switching users in the nav dropdown just swaps the stored value and reloads.

Admin powers (`DrMurloc` quick-register, parking spot assignment) gate on `_identity?.DisplayName == "DrMurloc"`. That's it. For a single-host weekend tool it's appropriate; for anything longer-lived, add ACA Built-in Auth (Entra ID) or a shared secret.

## See also

- [docs/UBIQUITOUS_LANGUAGE.md](docs/UBIQUITOUS_LANGUAGE.md) for what every name means
- [docs/EVENT_STORM.md](docs/EVENT_STORM.md) for every command-event-policy chain
- [docs/SAGA.md](docs/SAGA.md) for the parking allocation state machine
- [docs/decisions/](docs/decisions/) for the why behind every architectural call
