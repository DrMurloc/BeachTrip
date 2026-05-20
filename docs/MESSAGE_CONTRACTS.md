# Message Contracts

Every message that crosses a process boundary. Grouped by direction (commands in, events out) and aggregate. All messages are immutable records.

## Conventions

- **Commands** — present-tense imperatives (`RegisterAttendee`). Published by Web (or the Worker's CatalogSeeder); consumed by exactly one MT consumer in the Worker.
- **Domain events** — past-tense (`AttendeeRegistered`). Persisted as Cosmos documents in the `events` container; the `ProjectionWorker` reads them from the change feed.
- **Integration events** — past-tense, prefixed by domain (`ParkingSpotAllocated`). Published on the bus, consumed by zero-or-more MT consumers.
- **Saga messages** — inputs and outputs of the `ParkingAllocationStateMachine`. Inputs carry a `CorrelationId` field; outputs don't.

## Commands

### Attendees

| Command | Shape | Consumer |
|---|---|---|
| `RegisterAttendee` | `(AttendeeId, string DisplayName)` | `RegisterAttendeeConsumer` |
| `RenameAttendee` | `(AttendeeId, string DisplayName)` | `RenameAttendeeConsumer` |
| `DeclareAttendeeCar` | `(AttendeeId, int Capacity, ParkingPreference Preference)` | `DeclareAttendeeCarConsumer` |
| `UpdateAttendeeCarPreference` | `(AttendeeId, ParkingPreference Preference)` | `UpdateAttendeeCarPreferenceConsumer` |
| `DropAttendeeCar` | `(AttendeeId)` | `DropAttendeeCarConsumer` |

### Carpools

| Command | Shape | Consumer |
|---|---|---|
| `FormCarpool` | `(CarpoolId, AttendeeId DriverId, int CarCapacity, ParkingPreference Preference, IReadOnlyList<AttendeeId>? InitialPassengers)` | `FormCarpoolConsumer` |
| `JoinCarpool` | `(CarpoolId, AttendeeId)` | `JoinCarpoolConsumer` |
| `LeaveCarpool` | `(CarpoolId, AttendeeId)` | `LeaveCarpoolConsumer` |
| `ChangeCarpoolPreference` | `(CarpoolId, ParkingPreference Preference)` | `ChangeCarpoolPreferenceConsumer` |
| `DisbandCarpool` | `(CarpoolId, string Reason)` | `DisbandCarpoolConsumer` |

### Rooms

| Command | Shape | Consumer |
|---|---|---|
| `CreateRoom` | `(RoomId, string Name, int Capacity, bool IsLocked, IReadOnlyList<AttendeeId>? InitialOccupants)` | `CreateRoomConsumer` (only invoked by CatalogSeeder) |
| `AssignAttendeeToRoom` | `(RoomId, AttendeeId)` | `AssignAttendeeToRoomConsumer` |
| `RemoveAttendeeFromRoom` | `(RoomId, AttendeeId)` | `RemoveAttendeeFromRoomConsumer` |

### Parking (admin override)

| Command | Shape | Consumer |
|---|---|---|
| `ManuallyAssignSpot` | `(ParkingSpotId, ParkingClaim Claim)` | `ManuallyAssignSpotConsumer` |
| `RemoveSpotOverride` | `(ParkingSpotId)` | `RemoveSpotOverrideConsumer` |

## Domain events

Stored in Cosmos `events` container. Each has an `EventId` (Guid) and `OccurredAt` (DateTimeOffset) from the `DomainEvent` base record.

### Attendees

| Event | Shape |
|---|---|
| `AttendeeRegistered` | `(AttendeeId, string DisplayName)` |
| `AttendeeRenamed` | `(AttendeeId, string DisplayName)` |
| `AttendeeCarDeclared` | `(AttendeeId, int Capacity, ParkingPreference Preference)` |
| `AttendeeCarPreferenceUpdated` | `(AttendeeId, ParkingPreference Preference)` |
| `AttendeeCarDropped` | `(AttendeeId)` |

### Carpools

| Event | Shape |
|---|---|
| `CarpoolFormed` | `(CarpoolId, AttendeeId DriverId, int CarCapacity, ParkingPreference Preference, IReadOnlyList<AttendeeId> Members)` |
| `AttendeeJoinedCarpool` | `(CarpoolId, AttendeeId)` |
| `AttendeeLeftCarpool` | `(CarpoolId, AttendeeId)` |
| `CarpoolPreferenceChanged` | `(CarpoolId, ParkingPreference Preference)` |
| `CarpoolDisbanded` | `(CarpoolId, string Reason)` |

### Rooms

| Event | Shape |
|---|---|
| `RoomCreated` | `(RoomId, string Name, int Capacity, bool IsLocked, IReadOnlyList<AttendeeId> InitialOccupants)` |
| `AttendeeJoinedRoom` | `(RoomId, AttendeeId)` |
| `AttendeeLeftRoom` | `(RoomId, AttendeeId)` |

### Parking

| Event | Shape | Triggered by |
|---|---|---|
| `ParkingSpotCreated` | `(ParkingSpotId, string Name, ParkingSpotType Type, ParkingClaim? LockedClaim)` | CatalogSeeder |
| `ParkingSpotAssigned` | `(ParkingSpotId, ParkingClaim Claim)` | Bridge consumer applying saga's allocation |
| `ParkingSpotReleased` | `(ParkingSpotId)` | Bridge consumer applying saga's reclaim |
| `ParkingSpotManuallyAssigned` | `(ParkingSpotId, ParkingClaim Claim)` | `ManuallyAssignSpotConsumer` (admin) |
| `ParkingSpotOverrideRemoved` | `(ParkingSpotId)` | `RemoveSpotOverrideConsumer` (admin) |

## Integration events (cross-process)

Published on the bus for cross-process consumption (Web ↔ Worker). Not persisted; subscribers are MT consumers in any host.

### Read-model updates

| Event | Shape | Published by | Consumed by |
|---|---|---|---|
| `ViewUpdated` | `(string AggregateType, string AggregateId)` | `ProjectionWorker` after each view upsert | Web `ViewUpdatedConsumer` → `LiveUpdates` |

### Saga inputs (consumed by ParkingAllocationStateMachine)

| Event | Shape | Published by |
|---|---|---|
| `SeedParkingInventory` | `(Guid CorrelationId, IReadOnlyList<SpotSeed> Spots)` | (initial bootstrap, not currently called) |
| `CarpoolWantsParking` | `(Guid CorrelationId, CarpoolId, ParkingPreference)` | *not currently emitted* — see [SAGA.md](SAGA.md) |
| `CarpoolReleasesParking` | `(Guid CorrelationId, CarpoolId)` | *not currently emitted* |
| `SoloDriverWantsParking` | `(Guid CorrelationId, AttendeeId, ParkingPreference)` | *not currently emitted* |
| `SoloDriverReleasesParking` | `(Guid CorrelationId, AttendeeId)` | *not currently emitted* |
| `SpotTakenOutOfPool` | `(Guid CorrelationId, ParkingSpotId)` | `ManuallyAssignSpotConsumer` |
| `SpotReturnedToPool` | `(Guid CorrelationId, ParkingSpotId)` | `RemoveSpotOverrideConsumer` |

### Saga outputs

| Event | Shape | Consumed by |
|---|---|---|
| `ParkingSpotAllocated` | `(ParkingSpotId, ParkingClaim Claim)` | `ApplyParkingSpotAllocatedConsumer` (Worker) |
| `ParkingSpotReclaimed` | `(ParkingSpotId)` | `ApplyParkingSpotReclaimedConsumer` (Worker) |
| `SoloDriverBumped` | `(AttendeeId)` | Web `SoloDriverBumpedConsumer` → snackbar |
| `ClaimUnmet` | `(ClaimKind, Guid ClaimantId, ParkingPreference)` | *not currently consumed* — informational only |

## Value object shapes

### `ParkingClaim` (discriminated record)

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(Carpool), "carpool")]
[JsonDerivedType(typeof(Solo), "solo")]
abstract record ParkingClaim
{
    record Carpool(CarpoolId CarpoolId) : ParkingClaim;
    record Solo(AttendeeId AttendeeId) : ParkingClaim;
}
```

Wire format (Carpool variant):
```json
{ "$kind": "carpool", "carpoolId": "11111111-2222-3333-4444-555555555555" }
```

### `ParkingPreference` (enum)

```csharp
enum ParkingPreference { None = 0, Driveway = 1, Street = 2 }
```

Serialized as string via `JsonStringEnumConverter`.

### `SpotSeed` (saga payload)

```csharp
record SpotSeed(ParkingSpotId Id, string Name, ParkingSpotType Type, bool IsLocked, ParkingClaim? LockedClaim);
```

### Strongly-typed IDs

`AttendeeId`, `CarpoolId`, `RoomId`, `ParkingSpotId` — all `readonly record struct(Guid Value)`. Serialized as bare GUID strings via `StronglyTypedIdConverterFactory`. Wire format:
```
"a1000000-0000-0000-0000-000000000001"
```

## Delivery + ordering guarantees

- **RabbitMQ default delivery**: at-least-once. Consumers must be idempotent. Our aggregate `Apply` methods naturally are (same event applied twice = same final state); the `OverrideTo` / `RemoveOverride` methods short-circuit when the spot is already in the desired state.
- **No ordering guarantees across messages**. The saga handles this internally (single-instance serialization). The change-feed projector handles this via per-partition ordering (Cosmos guarantees ordered events per partition key, which is `aggregateId`).
- **Cosmos optimistic concurrency**: enforced by document-id collision. The CosmosEventRepository writes events with `id = "{type}|{aggId}|{version}"`; two writers racing to version N both try to create the same id and one gets a 409 → `ConcurrencyConflictException`.
