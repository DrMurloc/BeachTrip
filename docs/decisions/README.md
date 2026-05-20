# Architecture Decision Records

Short, dated records of the load-bearing architectural choices in this codebase. Format follows [Michael Nygard's template](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions): Status, Context, Decision, Consequences.

Add a new ADR by copying the next number and writing why-we-did-it-this-way *while you still remember*.

| # | Title | Status |
|---|---|---|
| [0001](0001-event-sourcing.md) | Event-source all aggregates | Accepted |
| [0002](0002-masstransit-mediator-and-bus.md) | MassTransit for both in-process mediator and cross-process bus | Accepted |
| [0003](0003-saga-process-manager.md) | Saga (process manager) for parking allocation | Accepted (dormant, see 0006) |
| [0004](0004-rabbitmq-over-azure-service-bus.md) | RabbitMQ instead of Azure Service Bus | Accepted (supersedes Phase 1 ASB choice) |
| [0005](0005-cosmos-db-with-change-feed.md) | Cosmos DB as event store + change-feed-driven projections | Accepted |
| [0006](0006-admin-override-replaces-saga.md) | Admin manual override replaces saga auto-allocation | Accepted (supersedes 0003 in practice) |
| [0007](0007-anonymous-viewing-no-auth.md) | Anonymous viewing + sign-in-by-handle, no real auth | Accepted |
| [0008](0008-optimistic-ui-updates.md) | Optimistic UI updates ahead of the projection | Accepted |
