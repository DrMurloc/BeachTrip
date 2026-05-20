# Ubiquitous Language

The names we use and what they mean. Pulled from the actual aggregates in `BeachTrip.Domain`. Use these terms in commits, in comments, in conversation. Cross-references are linked.

## Identity & people

**Attendee** — a single human attending the beach trip. Identified by an `AttendeeId` (record-struct over a `Guid`) and a freely-chosen `DisplayName`. Anonymous self-identification, no auth. May optionally have a [Car](#car). Aggregate root in [`Attendees/Attendee.cs`](../src/BeachTrip.Domain/Attendees/Attendee.cs).

**Handle** — the user-visible display name of an Attendee. The lobby's user switcher lists handles; the home page's autocomplete matches against existing handles when signing in.

**DrMurloc** — the host. A hardcoded admin name string. When `_identity?.DisplayName == "DrMurloc"` the UI unlocks the parking-spot assignment menus and the bulk-register quick-add field. Not a real role/permission system.

**Anonymous viewer** — a session with no identity stored in `ProtectedSessionStorage`. Can see the lobby (rooms, parking, carpools, who's where) but no action buttons appear.

## Vehicles

**Car** — a value object on an Attendee (`Car { Capacity, ParkingPreference }`). Capacity is the number of humans that fit. Preference is `None | Driveway | Street` and is currently ignored by the system (manually-assigned spots are the only allocation path).

**Driver** — an Attendee who has declared a Car. Required to [Form](#carpool) a [Carpool](#carpool).

**Solo driver** — an Attendee who has declared a Car but is not in any [active](#disbanded) Carpool. Eligible for a manually-assigned parking spot.

## Travel

**Carpool** — a group of 1+ Attendees travelling together in one Car. The driver always counts as a member, so a freshly-formed carpool with no passengers is a valid "I'm driving, hop in if you want" container. Aggregate root in [`Carpools/Carpool.cs`](../src/BeachTrip.Domain/Carpools/Carpool.cs). Invariants:
- `1 ≤ members.Count ≤ driver.Car.Capacity`
- Driver can't leave (must Disband instead)
- Passengers can come and go freely; the carpool stays active even when the driver is the only member left

**Driver** (in a Carpool context) — the Attendee whose Car the Carpool is using. Always the first member.

**Passenger** — non-driver member of a Carpool.

**Form** — the act of creating a Carpool. Requires the Driver to pre-select at least one Passenger.

**Disbanded** — a Carpool whose `IsActive` is false. Disbanded Carpools persist in event history but are hidden from the UI and no longer affect [parking allocation](#allocation).

## Sleeping

**Room** — a bedroom in the beach house with a [Capacity](#capacity) and a list of [Occupants](#occupant). Aggregate root in [`Rooms/Room.cs`](../src/BeachTrip.Domain/Rooms/Room.cs). Six of them, seeded by [`CatalogSeeder`](../src/BeachTrip.Infrastructure/Provisioning/CatalogSeeder.cs):
- 1F Queen (3)
- 2F Right (3, **locked** to DrMurloc + Iraiah + Murky permanently)
- 2F Left (5)
- 3F King (7 — King bed + 5 floor)
- 3F Double Twin (3)
- 3F Alcove (3)

**Capacity** (of a Room) — the maximum number of [Occupants](#occupant) it can hold. Includes both bed slots and floor space.

**Occupant** — an Attendee assigned to a Room. The lobby's UI enforces one-room-per-attendee by auto-leaving the current room when joining a new one.

**FreeSeats** — `Capacity - Occupants.Count`. Surfaced as a property on `RoomView` so the UI can render free-slot avatars.

**Locked room** — a Room whose `IsLocked` is true. Set at creation time only (no admin-unlock equivalent). Used for 2F Right to permanently reserve it for the host family.

## Parking

**ParkingSpot** — one of six physical parking spots at the house. Aggregate root in [`Parking/ParkingSpot.cs`](../src/BeachTrip.Domain/Parking/ParkingSpot.cs). Has a [Type](#parkingspottype), optional [Claim](#parkingclaim), and an `IsLocked` flag. Four driveway spots (Driveway-1 through Driveway-4) and two street spots (Street-1, Street-2).

**ParkingSpotType** — `Driveway` or `Street`. Visual differentiator in the UI (driveway tiles are blue-tinted, street tiles grey).

**ParkingClaim** — a discriminated record describing who has a ParkingSpot:
- `ParkingClaim.Carpool(carpoolId)` — the entire Carpool gets this spot
- `ParkingClaim.Solo(attendeeId)` — a single solo driver got it

Serialized with a `$kind` JSON discriminator.

**Locked spot** — a ParkingSpot whose `IsLocked` is true. Means an [admin override](#override) has been applied; the saga's allocator skips locked spots.

**Override** — an admin (DrMurloc) directly assigning a spot to a Claim, bypassing the saga's normal allocation logic. Sets `IsLocked = true` on the spot. Reversible via "Release override" in the per-spot kebab menu.

**Released override** — `IsLocked` flips back to false, claim is cleared, spot returns to the saga's allocation pool.

**Allocation** — the saga's automatic parking-spot assignment process. Currently dormant by design — see [SAGA.md](SAGA.md). When active, prioritizes Carpools over solo drivers and honors ParkingPreference as a soft hint.

**Bump** — when the saga reallocates and a solo driver who previously had a spot no longer does (because a Carpool took it). Triggers a `SoloDriverBumped` event that becomes a snackbar toast in that user's browser.

## Architectural plumbing

**Aggregate** — a transactional consistency boundary in the domain. Each Aggregate Root holds its own state, emits domain events, and is loaded/saved as a unit. Four of them: Attendee, Carpool, Room, ParkingSpot.

**Domain event** — an immutable record that something happened in the domain. Past-tense names (`AttendeeJoinedRoom`, not `JoinRoom`). Persisted to the Cosmos `events` container as the source of truth.

**Integration event** — a message published on the bus (RabbitMQ) for cross-process consumption. Distinct from domain events — integration events are how the saga + read-model projector + Web's LiveUpdates communicate.

**View** — a flat, denormalized read model maintained by the [ProjectionWorker](#projectionworker). One per aggregate type (AttendeeView, CarpoolView, RoomView, ParkingSpotView). Stored in dedicated Cosmos containers (`view-*`).

**Projection** — the act of building a View from domain events. Happens in `ProjectionWorker.cs` via Cosmos change feed.

**Change feed** — Cosmos DB's append-log of every document change, exposed via a long-polling SDK API. The ProjectionWorker subscribes to the `events` container's change feed and updates Views accordingly.

**Saga** — short for "process manager" in MassTransit terms. A long-lived stateful entity that reacts to events and issues commands to coordinate work across aggregates. We have one: `ParkingAllocationStateMachine` — see [SAGA.md](SAGA.md).

**ProjectionWorker** — the hosted service in `BeachTrip.Worker` that runs the change-feed processor and dispatches each event to its projection handler.

**LiveUpdates** — a singleton C# event broker in the Web process. UI consumers (one per browser circuit) push `ViewUpdated` messages into it; subscribed components fire `StateHasChanged()` so the browser re-renders.

**Bridge consumer** — a Worker-hosted MassTransit consumer that translates a saga decision (e.g. `ParkingSpotAllocated`) into a domain command (`ParkingSpot.AssignToCarpool`). Lives in `ApplyAllocationConsumers.cs`.

## Things that aren't terms

These show up in the codebase but aren't part of the ubiquitous language — they're implementation details:

- **DCP** (Distributed Container Platform) — Aspire's local container orchestrator. Implementation detail of `dotnet run --project AppHost`.
- **ServiceDefaults** — Aspire convention for shared service config (OTel, health, resilience). Project-level helper.
- **EventStore** — the Cosmos `events` container. Sometimes called "the event store" in conversation but the type is just `Container`.
- **WarmupService** — `CosmosWarmupService` in Web. Defensive infra; not a domain concept.
