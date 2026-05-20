# Event Storm

The domain laid out in event-storming notation. If you've never seen this style: imagine a wall covered in colored sticky notes. Orange = events that happened in the domain. Blue = commands that caused them. Purple = policies that react to events. Yellow = read models. Read each chain top-to-bottom.

```
🟦 COMMAND   →   🟧 EVENT   →   🟪 POLICY   →   🟦 next command (...)
                              ↓
                              🟨 READ MODEL
```

## Attendee

```
🟦  RegisterAttendee
      ↓ (Attendee.Register)
🟧  AttendeeRegistered
      ↓ Cosmos change feed
🟪  ProjectionWorker.ProjectAttendee
      ↓ writes
🟨  AttendeeView (view-attendees container)
      ↓ ProjectionWorker.Publish
🟧  ViewUpdated("Attendee", id)
      ↓ Web ViewUpdatedConsumer
🟪  LiveUpdates.NotifyViewUpdated → MainLayout + Lobby refresh
```

```
🟦  RenameAttendee
      ↓
🟧  AttendeeRenamed
      ↓ (same projection / live-update chain as above)
```

```
🟦  DeclareAttendeeCar  (capacity, preference)
      ↓ (Attendee.DeclareCar — fails if HasCar)
🟧  AttendeeCarDeclared
      ↓ projection
🟨  AttendeeView.{CarCapacity, CarPreference} populated
```

```
🟦  UpdateAttendeeCarPreference
      ↓
🟧  AttendeeCarPreferenceUpdated
```

```
🟦  DropAttendeeCar
      ↓
🟧  AttendeeCarDropped
      ↓ projection
🟨  AttendeeView.CarCapacity / CarPreference → null
```

## Carpool

```
🟦  FormCarpool  (carpoolId, driverId, carCapacity, preference, passengers)
      ↓ (Carpool.Form — invariant: 2 ≤ members ≤ carCapacity)
🟧  CarpoolFormed
      ↓ projection
🟨  CarpoolView (new doc in view-carpools)
```

```
🟦  JoinCarpool  (carpoolId, attendeeId)
      ↓ UI auto-leaves prior carpool first
🟦  LeaveCarpool  (priorCarpoolId, attendeeId)    ← issued by lobby before Join
      ↓
🟧  AttendeeLeftCarpool        ← prior carpool
      ↓ if members < 2
🟧  CarpoolDisbanded            ← auto-disband
      ↓ projection: priorCarpool.IsActive = false
🟨  CarpoolView (prior)

🟦  JoinCarpool                  ← then the original join
      ↓ (Carpool.AddMember — fails if over capacity)
🟧  AttendeeJoinedCarpool
      ↓ projection
🟨  CarpoolView (target)
```

```
🟦  LeaveCarpool
      ↓
🟧  AttendeeLeftCarpool
      ↓ if remaining members < 2
🟧  CarpoolDisbanded
```

```
🟦  ChangeCarpoolPreference
      ↓
🟧  CarpoolPreferenceChanged
```

```
🟦  DisbandCarpool
      ↓
🟧  CarpoolDisbanded
      ↓ projection: IsActive = false
🟨  CarpoolView (hidden from lobby)
```

## Room

```
🟦  CreateRoom    ← only emitted by CatalogSeeder at startup
      ↓
🟧  RoomCreated
      ↓ projection
🟨  RoomView
```

```
🟦  AssignAttendeeToRoom  (roomId, attendeeId)
      ↓ UI auto-leaves prior room first
🟦  RemoveAttendeeFromRoom  (priorRoomId, attendeeId)
      ↓
🟧  AttendeeLeftRoom
      ↓ projection
🟨  RoomView (prior)

🟦  AssignAttendeeToRoom
      ↓ (Room.AddOccupant — fails if room full or locked)
🟧  AttendeeJoinedRoom
      ↓ projection
🟨  RoomView (target)
```

```
🟦  RemoveAttendeeFromRoom
      ↓
🟧  AttendeeLeftRoom
```

## Parking — admin-driven path (current default)

```
🟦  ManuallyAssignSpot  (spotId, ParkingClaim)
      ↓ (ParkingSpot.OverrideTo — sets IsLocked=true)
🟧  ParkingSpotManuallyAssigned
      ↓ projection
🟨  ParkingSpotView.{IsLocked: true, Claim: ...}

🟪  ManuallyAssignSpotConsumer also publishes →
🟧  SpotTakenOutOfPool  (saga input message)
      ↓ saga state update
   ParkingAllocationState.Spots[idx].IsLocked = true
   (saga's reallocator now skips this spot)
```

```
🟦  RemoveSpotOverride
      ↓
🟧  ParkingSpotOverrideRemoved
      ↓ projection
🟨  ParkingSpotView.{IsLocked: false, Claim: null}

🟪  RemoveSpotOverrideConsumer also publishes →
🟧  SpotReturnedToPool  (saga input message)
      ↓ saga state update + Reallocate()
```

## Parking — saga path (dormant)

The saga's auto-allocation chain. Currently no command consumers publish `*WantsParking` events, so this chain is idle. The full machinery is wired and tested — flipping it back on requires reinstating the `Bus.Publish(new CarpoolWantsParking(...))` lines in `FormCarpoolConsumer` / `ChangeCarpoolPreferenceConsumer` / `DeclareAttendeeCarConsumer`.

```
🟦  FormCarpool
      ↓ (FormCarpoolConsumer, if Preference != None)
🟧  CarpoolWantsParking          ← NOT currently emitted
      ↓ saga input
🟪  ParkingAllocationStateMachine.EnqueueAndReallocate
      ↓ runs ParkingAllocator (priority + preference)
🟧  ParkingSpotAllocated
      ↓ bridge consumer
🟦  (synthesizes) ParkingSpot.AssignToCarpool
      ↓
🟧  ParkingSpotAssigned
      ↓ projection
🟨  ParkingSpotView.Claim populated
```

```
🟦  DisbandCarpool / LeaveCarpool (when carpool dies)
      ↓
🟧  CarpoolReleasesParking       ← NOT currently emitted
      ↓ saga: dequeue + reallocate
🟧  ParkingSpotReclaimed         ← if spot was held
      ↓ bridge consumer
🟦  (synthesizes) ParkingSpot.Release
      ↓
🟧  ParkingSpotReleased
```

```
🟪  When the reallocator picks a different winner for an occupied spot:
🟧  SoloDriverBumped  (attendeeId)
      ↓ Web SoloDriverBumpedConsumer
🟪  if it's me → ISnackbar.Add("A carpool just took your parking spot.")
```

## Lifecycle bootstrap

```
On Worker startup:
🟪  CatalogSeeder.StartAsync
      ↓ if not already seeded
🟦  RegisterAttendee × 3  (DrMurloc, Iraiah, Murky)
🟦  Attendee.DeclareCar (DrMurloc only, capacity=3, Driveway)
🟦  CreateRoom × 6       (1F Queen, 2F Right [locked], 2F Left, 3F King, 3F Twin, 3F Alcove)
🟦  ParkingSpot.Create × 6  (Driveway-1 through 4, Street-1 + 2)
🟪  ProjectionWorker.StartAsync
      ↓ subscribes to events container's change feed
   On every change → re-read aggregate → upsert View → publish ViewUpdated
```

## Catalog of read models

| View | Container | Built from |
|---|---|---|
| `AttendeeView` | `view-attendees` | Attendee aggregate |
| `CarpoolView` | `view-carpools` | Carpool aggregate |
| `RoomView` | `view-rooms` | Room aggregate |
| `ParkingSpotView` | `view-parking-spots` | ParkingSpot aggregate |

All four follow the same pattern: re-read the aggregate via its repo (which replays all events), map to a flat DTO, upsert into Cosmos partitioned by `/id`.

## Catalog of policies

Policies (in the event-storming sense) are reactive — they listen for events and decide what to do next. Implementation-wise they're either bridge consumers, the ProjectionWorker, or the saga.

| Policy | Triggered by | Issues |
|---|---|---|
| **Refresh views** | Any domain event in `events` container | View upsert + `ViewUpdated` |
| **Notify browsers** | `ViewUpdated` | `LiveUpdates.ViewChanged` C# event → SignalR push |
| **Apply spot allocation** | `ParkingSpotAllocated` (saga) | `ParkingSpot.AssignTo*` aggregate call |
| **Apply spot release** | `ParkingSpotReclaimed` (saga) | `ParkingSpot.Release` aggregate call |
| **Mark spot out of pool** | `ManuallyAssignSpot` command | `SpotTakenOutOfPool` to saga |
| **Mark spot back in pool** | `RemoveSpotOverride` command | `SpotReturnedToPool` to saga |
| **Toast on bump** | `SoloDriverBumped` (saga) | `ISnackbar.Add` (only if it's the current user) |
