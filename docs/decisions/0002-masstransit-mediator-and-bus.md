# 2. MassTransit for both in-process mediator and cross-process bus

Date: 2026-05-15

## Status

Accepted.

## Context

The system has two distinct messaging needs:

1. **In-process dispatch** in the Web — commands like `JoinCarpool` need to land on a consumer that loads an aggregate, runs the operation, persists events. Classic mediator pattern; could be MediatR or a hand-rolled `IRequestHandler<T>`.
2. **Cross-process messaging** between Web and Worker — the saga lives in Worker, the UI lives in Web, view-update notifications fan out from Worker to all Web circuits. Needs a real broker.

We considered:

- **MediatR in Web + MassTransit on the bus for cross-process** — two abstractions, two registration models, two test setups. Commands would have to be re-encoded as bus messages at the seam.
- **MassTransit Mediator everywhere** — single abstraction, single consumer model, single test harness, single set of conventions. MassTransit ships an in-process [`IMediator`](https://masstransit.io/documentation/concepts/mediator) that uses the same consumer base class as the bus.

MassTransit 8.5.9 is the last fully free version before commercial v9. We pinned to 8.5.x explicitly in `Directory.Packages.props` and noted this in the README so a future `dotnet outdated` doesn't accidentally cross the paywall.

## Decision

MassTransit is the only messaging abstraction in the system. Consumers register once. Whether they're invoked via in-process mediator or via the bus depends on configuration, not on the consumer code.

In practice the split is simpler than that: **all consumers run in the Worker**, dispatched via the bus from the Web. We don't currently use the in-process mediator at all — but keeping the option open meant we never had to design around two patterns. The Web publishes commands via `IBus.Publish(...)` and trusts the broker to deliver to the Worker's consumer.

- One `services.AddMassTransit(...)` registration per host.
- Consumers and the saga state machine live in `BeachTrip.Application`.
- Web's `AddMassTransit` registers no consumers — it's a publisher only.
- Worker's `AddMassTransit` registers every consumer and the saga.

## Consequences

**Good**

- One mental model for messaging. New contributors learn "consumer + `Consume(ConsumeContext<T>)`" once.
- Free integration testing via `InMemoryTestHarness` — saga tests use the same registration as production.
- If the Web grows a need for fully synchronous command handling later, we can flip a flag and route to an in-process mediator without rewriting consumers.
- MT's pipeline gives us retries, dead-letter queues, OpenTelemetry instrumentation, and `IPublishEndpoint` semantics for free.

**Bad**

- For genuinely local operations (e.g. a Web-only validation handler), MT is overkill — would just be a method call. We don't have any of those yet, but if we did, the temptation to "send a message to ourselves" would be real and bad.
- MT 8.x is on a maintenance footing as v9 goes commercial. We may need to re-evaluate before any major upgrade.
- Coupling consumers to MT's `ConsumeContext<T>` makes them hard to call from non-MT code. We mitigate by keeping the consumer body to ~5 lines of "load aggregate, mutate, save."
