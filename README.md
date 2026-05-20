# BeachTrip

> Over-engineered rooms-and-parking app for a 4-day beach house gathering.

A weekend tool to help a small group self-organize sleeping arrangements and parking at a shared house. The actual problem is 6 rooms + 6 parking spots + 10ish people. The codebase is event-sourced tactical DDD on a distributed message bus because we wanted to be. Build deliberately.

```
.NET 10  •  Aspire 13  •  Blazor Server + MudBlazor  •  MassTransit on RabbitMQ
Cosmos DB (event store + change-feed projections)  •  OpenTelemetry  •  azd-deployable
```

## What's in here

- **4 aggregates** (Attendee, Carpool, Room, ParkingSpot) with strongly-typed record-struct IDs and 18 domain events
- **19 MassTransit consumers** spread across commands, saga bridges, and UI notifications
- **1 process-manager saga** for parking allocation (priority-based, currently dormant — admin does the assigning)
- **4 read-model projections** kept fresh by the Cosmos change feed
- **58 unit tests** — every aggregate invariant + the saga end-to-end through MT's in-memory harness
- **Two-process topology** (Web + Worker) over RabbitMQ
- **`azd up`** to deploy the whole thing to Azure Container Apps

## Documentation

| Doc | What it's for |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Layer cake, write/read paths, distributed topology, diagrams |
| [DEPLOYMENT.md](DEPLOYMENT.md) | Local Aspire run, `azd up`, troubleshooting |
| [docs/UBIQUITOUS_LANGUAGE.md](docs/UBIQUITOUS_LANGUAGE.md) | DDD glossary — the names we use and what they mean |
| [docs/EVENT_STORM.md](docs/EVENT_STORM.md) | Event-storming notation: every command → event → policy chain |
| [docs/SAGA.md](docs/SAGA.md) | The parking allocation state machine + allocator algorithm |
| [docs/MESSAGE_CONTRACTS.md](docs/MESSAGE_CONTRACTS.md) | Reference of every command, domain event, and integration event |
| [docs/SCENARIOS.feature](docs/SCENARIOS.feature) | Gherkin acceptance flows |
| [docs/decisions/](docs/decisions/) | Architecture Decision Records (ADRs) |

---

## Getting started

Two paths. Pick the one matching what you've already got installed.

### Path A — from zero

You need .NET 10, Docker, Git, and (optionally) `azd` to deploy.

```pwsh
# Windows: install everything via winget
winget install Microsoft.DotNet.SDK.10
winget install Docker.DockerDesktop
winget install Git.Git
winget install Microsoft.Azd                 # only if you plan to deploy

# Aspire's project templates aren't shipped with the SDK; install them once:
dotnet new install Aspire.ProjectTemplates@13.3.4

# Start Docker Desktop manually if it isn't already running.
```

Then jump to **Run it locally** below.

> macOS: replace `winget` with `brew install dotnet docker git azure-dev`. Linux: use your package manager + `wget`-the-dotnet-installer for .NET 10.

### Path B — already have basics (.NET 10, Docker, Git)

You only need the Aspire templates if you don't already have them:

```pwsh
dotnet new install Aspire.ProjectTemplates@13.3.4
```

### Run it locally

```pwsh
git clone https://github.com/DrMurloc/BeachTrip.git
cd BeachTrip
dotnet run --project src/BeachTrip.AppHost
```

First run takes a couple of minutes — Aspire pulls the RabbitMQ + Cosmos emulator images (~4 GB combined) and the Worker waits for Cosmos to be query-ready.

When you see:

```
Login to the dashboard at https://localhost:17188/login?t=<token>
Distributed application started.
```

Open the dashboard URL. You'll see four resources (web, worker, rabbitmq, cosmos) — click into `web` to grab its external URL, then open that in **two different browsers** (regular + incognito works). Each browser registers as a separate handle and you can watch the live-update plumbing in action.

The seeded admin handle is **DrMurloc** — sign in with that exact name to unlock the parking-spot assignment menus + the bulk-register quick-add field.

### Run the tests

```pwsh
dotnet test
```

58 tests, ~3 seconds.

### Deploy to Azure

See [DEPLOYMENT.md](DEPLOYMENT.md). Short version:

```pwsh
azd auth login --tenant <your-tenant-id>
azd init       # auto-detects the AppHost
azd up         # provisions + deploys, ~8-15 min the first time
```

Code-only redeploys after that:

```pwsh
azd deploy
```

---

## When something goes wrong locally

Three ways the local stack can wedge. The fix is the same each time — kill everything and restart.

```pwsh
# Save this as a PowerShell function in your $PROFILE
function Reset-Aspire {
    Get-Process dcp -ErrorAction SilentlyContinue | Stop-Process -Force
    Get-Process dotnet -ErrorAction SilentlyContinue | Where-Object {
        (Get-CimInstance Win32_Process -Filter "ProcessId=$($_.Id)").CommandLine -match 'Aspire|BeachTrip'
    } | Stop-Process -Force
    $c = docker ps -aq
    if ($c) { docker rm -f $c | Out-Null }
    "Aspire reset."
}
```

Then `Reset-Aspire; dotnet run --project src/BeachTrip.AppHost`.

The thing to avoid: closing Visual Studio's debug session via the X on the tab or letting the IDE crash. Always **Stop Debugging (Shift+F5)** — that lets Aspire's DCP tear containers down in order. Skipping that leaves orphan DCP processes and stale containers, which is the #1 source of "why can't I rebuild" file-lock errors.

---

## License

For my family. No license, no support, no warranty, no Sundays.
