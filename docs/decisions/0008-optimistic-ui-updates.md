# 8. Optimistic UI updates ahead of the projection

Date: 2026-05-16

## Status

Accepted.

## Context

The canonical "click → see result" path for any action (join a room, form a carpool, assign a spot) goes:

```
click
  → IBus.Publish(command)        ← Web → broker
  → consumer in Worker           ← broker → Worker
  → aggregate.Load + mutate + Save  ← Worker ↔ Cosmos events container
  → Cosmos change feed fires      ← change feed processor
  → ProjectionWorker upserts view ← Worker ↔ Cosmos view container
  → IBus.Publish(ViewUpdated)    ← Worker → broker
  → ViewUpdatedConsumer in Web   ← broker → Web
  → LiveUpdates → SignalR push   ← Web → browser
  → StateHasChanged → re-render
```

That's ~8 network hops. On the dev box it takes 2-3 seconds end-to-end. User watching the screen sees: click → nothing → nothing → still nothing → finally, room shows you as occupant. Feels broken even when it isn't.

User reported this directly: *"Joining and leaving rooms takes like, 3 seconds to register."*

Options:

1. **Optimize the chain** — fewer hops, faster Cosmos, etc. Diminishing returns; the chain is *correct* and we don't want to compromise the architecture for 2 seconds.
2. **Disable the chain and make Web mutate Cosmos directly** — defeats the whole CQRS + projection point.
3. **Optimistic UI** — apply the change locally in the Blazor component *immediately* on click, then let the real `ViewUpdated` reconcile when it arrives. If the command fails, surface the error and revert.

Optimistic UI is what every well-tuned web app does (Gmail, Linear, Slack). The system is designed around eventually-consistent reads anyway — the projection model already promises "view will catch up." Optimism just leans into that promise client-side.

## Decision

Each Blazor page that issues commands mutates its locally-held view state *before* publishing the command. The published command and the eventual `ViewUpdated` propagate through the full chain in the background. When `ViewUpdated` arrives, the page re-reads from the view store and replaces the local state — reconciling the optimistic guess with truth.

In practice:

- `Lobby.razor` holds `_rooms`, `_carpools`, `_spots`, `_attendees` lists in component state.
- On "Join room": `_rooms.First(r => r.Id == roomId).Occupants.Add(_identity)` runs synchronously, then `Bus.Publish(new AssignAttendeeToRoom(...))` returns immediately.
- `LiveUpdates.ViewChanged` event fires when the projection completes; the handler calls `ReloadAsync()` which fetches all views fresh and calls `StateHasChanged()`.

Failure handling is currently minimal: if the command fails on the consumer side (e.g. domain invariant violated), the optimistic mutation stays in the UI until the next reload. We accept this because:

- Most failures are programmer errors we'd catch in dev (over-capacity, etc.).
- The UI prevents most invalid actions in the first place (no Join button on full rooms, etc.).
- A periodic refresh + the next user action's reload corrects any drift.

For more rigorous failure handling we'd track a `pendingOptimisticChange` and roll back if the matching `ViewUpdated` doesn't arrive within a timeout. The 4-day-trip scope doesn't justify that yet.

## Consequences

**Good**

- Clicks feel instant. No more 3-second "did I miss it?" moments.
- The reconciliation pattern is honest about the system's eventual consistency rather than hiding it behind synchronous-looking calls.
- The architecture stays clean. No special "fast path" in the command pipeline; the bus and projector keep doing exactly what they did, just with the UI no longer waiting for them.

**Bad**

- Two sources of truth in the page state for ~1 second: the optimistic local mutation and the eventual server state. If they diverge (e.g. someone else also acted in the gap), the user sees a brief flicker as the view reconciles.
- No automatic rollback on command failure. A user could optimistically join a room that the consumer then refuses to add them to (e.g. race against another user filling the last seat) and the UI would still show them as occupant until the next reload. Acceptable for our scale because such races are rare and the next action fixes it; not acceptable for higher-stakes systems.
- Components must hold mutable local copies of view DTOs. This is mildly un-Blazor-idiomatic (Blazor tends to assume server-rendered state). The local copies stay in scope for the page lifecycle so it's not memory-leaky, just a slightly chunkier component than a pure-rendering one.
