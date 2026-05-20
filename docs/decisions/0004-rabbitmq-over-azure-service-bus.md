# 4. RabbitMQ instead of Azure Service Bus

Date: 2026-05-16

## Status

Accepted. Supersedes the original transport choice (Azure Service Bus) made during Phase 1.

## Context

The initial architecture used Azure Service Bus for production-grade messaging in Azure, with `Aspire.Hosting.Azure.ServiceBus`'s `RunAsEmulator()` for local development. The plan was:

- Local: ASB emulator container, configured via Aspire.
- Azure: real Service Bus namespace, provisioned via `azd up`.
- One transport, one set of MassTransit configuration calls (`UsingAzureServiceBus`).

We got Phase 1 + Phase 2 working with this stack. Cosmos was already an emulator and was fine. ASB-emulator looked the same on paper.

It wasn't.

When MassTransit starts against a Service Bus namespace it uses two clients:

1. **`ServiceBusClient`** — AMQP, for actual message send/receive.
2. **`ServiceBusAdministrationClient`** — *HTTPS*, for topology management (creating queues, topics, subscriptions).

Aspire's ASB-emulator container exposes only the AMQP listener via the DCP proxy. MassTransit's startup unconditionally calls the admin client to ensure topology exists. The admin client tries HTTPS, fails, retries with exponential backoff, and the bus never becomes ready. Symptom: "retry storm on start" with `ServiceBusException` chains in the log.

This isn't a configuration mistake we could fix. The emulator literally does not implement the admin HTTPS endpoint, and MT does not have an option to skip topology creation.

Options at that point:

1. **Stay on ASB, skip the emulator** — develop against a real Azure namespace. Costs money during dev, breaks offline work.
2. **Stay on ASB, write our own topology bootstrap** — fork or wrap MT's startup to no-op admin calls. Possible but ugly.
3. **Switch to RabbitMQ** — has a great emulator story (it's just RabbitMQ), is a first-class MT transport, runs trivially in Aspire via `AddRabbitMQ`.

User's exact words: *"Just do rabbit. I like rabbit. Fuck Azure Service Bus."*

## Decision

Use RabbitMQ as the bus transport, both locally and in Azure.

- Local: `builder.AddRabbitMQ("rabbitmq").WithManagementPlugin()` in `AppHost.cs`. The management UI on port 15672 is genuinely useful during debugging — you can watch queues fill up.
- Azure: same `rabbitmq` resource is provisioned by Aspire as a container in Azure Container Apps (no managed RabbitMQ on Azure, but a container is fine for our scale).

MassTransit configuration changes from `UsingAzureServiceBus` to `UsingRabbitMq` in both Web and Worker. Connection string flows via Aspire as `ConnectionStrings:rabbitmq`.

## Consequences

**Good**

- Local dev works without internet. The emulator is the real product.
- The MT pipeline is identical; consumer code didn't change a line.
- RabbitMQ's management UI is a debugging multiplier — visible queues, message rates, dead-letter inspection, all in a browser.
- Container Apps + RabbitMQ container is a known-good combination. `azd up` provisions it without issues.

**Bad**

- We lost the "real Azure native service" credibility points. Doesn't matter for this app, would matter for a real production system.
- RabbitMQ-as-a-container has no managed durability story on Azure — if the container restarts and we hadn't configured a persistent volume, queue contents vanish. For a 4-day trip with idempotent consumers, fine. For real prod, we'd want either Azure managed RabbitMQ (preview), a hosted offering (CloudAMQP), or to swap to ASB *for real* (not the emulator).
- We are now committed to RabbitMQ semantics (exchange/queue routing) rather than ASB semantics (topics/subscriptions). MT abstracts most of this away, but transport-specific knobs would differ.

## Notes

The emulator's missing admin endpoint is logged as a known limitation in the MT docs (it predates the Aspire integration). We didn't waste much time once we saw the symptom; the swap took an hour including verifying every consumer still worked end-to-end.
