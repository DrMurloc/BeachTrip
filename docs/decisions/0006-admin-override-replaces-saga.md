# 6. Admin manual override replaces saga auto-allocation

Date: 2026-05-17

## Status

Accepted. Supersedes (in practice) the auto-allocation behavior of [ADR-0003](0003-saga-process-manager.md). The saga itself remains fully implemented and tested — only its inputs are no longer emitted.

## Context

The original parking model was: declare your car with a `ParkingPreference` (`Driveway` / `Street` / `None`), the [saga](../SAGA.md) allocates a spot based on priority (carpools beat solos) and preference (matched type beats fallback). When a carpool formed, a previously-parked solo driver might be bumped — they'd see a snackbar toast and free up their spot.

This worked end-to-end. The first two days of using it in practice (during testing) surfaced a different problem: **the host (DrMurloc) knows the parking layout better than any allocator can**. He knew which driveway spot was easiest to back out of, which street spot was furthest from the trash cans, which slot was reserved for the guest with the mobility issue. The auto-allocator made consistent, fair decisions; they just weren't always the *right* decisions for this physical place.

User's exact words: *"Lets get rid of auto assigning. Don't let the user pick, let them register the carpool. DrMurloc will assign."*

## Decision

Strip the auto-allocation path while keeping the saga and bridge consumers wired and tested.

Concretely:

1. **Stop emitting** `CarpoolWantsParking`, `CarpoolReleasesParking`, `SoloDriverWantsParking`, `SoloDriverReleasesParking` from the command consumers (`FormCarpoolConsumer`, `DeclareAttendeeCarConsumer`, `DisbandCarpoolConsumer`, `LeaveCarpoolConsumer`, `ChangeCarpoolPreferenceConsumer`, `DropAttendeeCarConsumer`).
2. **Remove the parking-preference inputs** from the lobby UI. The car declaration is now just a capacity number; the carpool form is just passengers.
3. **Add admin commands**: `ManuallyAssignSpot(spotId, ParkingClaim)` and `RemoveSpotOverride(spotId)`. New methods on `ParkingSpot`: `OverrideTo(claim)` sets `IsLocked = true` and applies the claim; `RemoveOverride()` clears both.
4. **Admin UI**: each parking spot card grows a kebab (⋮) menu visible only to DrMurloc. The menu lists every active carpool and every car-having attendee; selecting one assigns that claim. The menu also has "Release" when a spot is currently overridden.
5. **Keep the saga aware**: `ManuallyAssignSpotConsumer` publishes `SpotTakenOutOfPool(spotId)` to the saga; `RemoveSpotOverrideConsumer` publishes `SpotReturnedToPool(spotId)`. The saga marks the spot as locked in its inventory and skips it in any future allocation. If we ever re-enable auto-allocation, the two paths coexist cleanly.

The saga's full test suite (`ParkingAllocatorTests`, `ParkingAllocationSagaTests`) still passes. The state machine, the allocator, the bridge consumers, the `SoloDriverBumped` notification path — all wired, all green. Just no upstream events feed the queue.

## Consequences

**Good**

- The host has full, explicit control. No surprises like "the allocator picked Driveway-3 for the guest who needed Driveway-1."
- The lobby UI got simpler: car declaration shrank from `capacity + preference` to just `capacity`; carpool formation lost its preference picker.
- The saga's machinery serves as documentation of a sophisticated allocator that the team can re-enable if the requirements change. The over-engineering is preserved with zero ongoing cost.
- Admin actions still flow through the same command/event/projection chain as everything else — there's no parallel write path. The override is just an aggregate operation with a feature flag (`IsLocked`).
- The bridge from admin overrides to saga state (`SpotTakenOutOfPool` / `SpotReturnedToPool`) means the dormant saga's view of inventory stays consistent with the override state. Re-enabling auto-allocation later doesn't require a state rebuild.

**Bad**

- We carry a tested-but-unfed code path. New contributors will see the saga, the allocator, the bridge consumers, the bumped-toast UI — all wired — and wonder if they should be doing something. Resolution: [SAGA.md](../SAGA.md) explicitly labels the saga as dormant and gives a 5-line reinstatement recipe.
- The `SoloDriverBumped` snackbar pathway is also dormant. We kept it because re-enabling needs the wire, and toasting an unexpected "you got bumped" message would be the only way a user could discover an auto-allocation decision happened against them — so it stays plumbed.
- An admin who isn't around can't delegate. There's no "I'm DrMurloc for the next hour" mechanism. For a 4-day trip with one host, fine; if this generalized, the saga would come back on.

## Re-enabling

See [SAGA.md § Reactivating auto-allocation](../SAGA.md#reactivating-auto-allocation). Five `await ctx.Publish(new *WantsParking(...))` lines re-wire the inputs, the preference inputs come back in the lobby UI, and the saga starts allocating again. The admin override path keeps working alongside.
