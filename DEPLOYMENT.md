# Deployment

Two modes: local (Aspire + Docker) and Azure (`azd`).

## Local

### Prerequisites

- .NET 10 SDK
- Docker Desktop (running)
- Aspire project templates: `dotnet new install Aspire.ProjectTemplates@13.3.4`

### Run

```pwsh
dotnet run --project src/BeachTrip.AppHost
```

Wait ~30s for the Aspire dashboard URL to appear, then ~1-2 min for the Cosmos emulator to be query-ready (the seeder will retry-log while it waits).

The dashboard shows four resources:

| Resource | What | URL |
|---|---|---|
| **web** | Blazor Server UI | `https://localhost:<dynamic>` (click through the dashboard) |
| **worker** | Background service host | (no external URL) |
| **rabbitmq** | Message broker + management UI | management at `http://localhost:<dynamic>` |
| **cosmos** | Cosmos emulator + data explorer | data explorer linked from the dashboard |

### Stop

**Always Ctrl+C in the terminal you launched from, or Shift+F5 in Visual Studio.** Aspire's DCP needs the graceful-shutdown signal to tear containers down in order.

If you kill the process the wrong way you'll get orphan DCP processes and stale containers. To reset:

```pwsh
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

Drop that in your `$PROFILE` and `Reset-Aspire` becomes a one-word recovery button.

---

## Azure (via `azd`)

### Prerequisites

- Azure subscription with permission to create resource groups + Container Apps + Cosmos
- Azure Developer CLI: `winget install Microsoft.Azd`
- `azd auth login --tenant <tenant-id>` — needed if your tenant requires MFA

### First-time setup

```pwsh
azd init    # interactive — picks up the Aspire AppHost automatically;
            # you pick an env name like "beach" and an Azure region.
azd up      # provisions infra + builds + pushes images + deploys.
            # 8-15 min the first time. Mostly the ACR build.
```

What gets created in your subscription:

- A new **Resource Group** named after your environment
- An **Azure Container Apps Environment** (managed Kubernetes-shaped)
- Three **Container Apps**: `web` (external HTTPS ingress), `worker`, `rabbitmq`
- An **Azure Cosmos DB account** with the `beachtrip` database and its 8 containers
- An **Azure Container Registry** to hold the built images
- A **Log Analytics workspace** + **Application Insights** for OpenTelemetry data

When it finishes, `azd show` prints the public URL of the `web` app.

### Subsequent deploys

After the first `azd up`, code-only changes redeploy with:

```pwsh
azd deploy           # builds + pushes + deploys all services, ~2 min
azd deploy web       # just one service
azd deploy worker
```

Use `azd up` again only when you've changed `AppHost.cs` in a way that affects infrastructure (added a resource, changed a port binding, swapped a transport).

### Useful follow-ups

```pwsh
azd monitor          # opens the Aspire dashboard for the Azure deployment
azd show             # prints URLs + resource info
azd env get-values   # prints all environment variables azd is injecting
azd down             # tears the entire environment down — use to clean up after the trip
```

### Cost expectations for a 4-day app

Mostly free-tier territory if you stay under the limits:

- **Container Apps**: free tier covers ~180k vCPU-seconds/month; this app at idle is well under
- **Cosmos DB**: free tier covers 1000 RU/s + 25 GB; this app generates a few hundred events total
- **App Insights**: free tier 5 GB/month
- **ACR**: Basic tier ~$5/month (the one paid component)
- **RabbitMQ on ACA**: ~$5-10/month at the smallest profile

Realistic total for 4 days: **$0-3** depending on whether you keep ACR around.

> ⚠️ Cosmos free tier is **one per subscription**. If you've already used it elsewhere, this account will be paid (~$24/month minimum). Check `azd up`'s plan output before approving.

### Tearing down after the trip

```pwsh
azd down --purge --force
```

The `--purge` flag also removes soft-deleted resources (Cosmos and Container Apps have soft-delete by default). Without it you'd keep paying for the storage.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| "File is locked by BeachTrip.Web" during build | Local AppHost or VS-launched session still running | `Reset-Aspire` (see above) |
| Containers not appearing in `docker ps` after AppHost start | Cosmos pulled the wrong port range, RabbitMQ still pulling | Wait — first run pulls ~6 GB of images. Watch with `docker pull` or `docker images` |
| `Could not load file or assembly 'MassTransit.Azure.Cosmos'` (or any package) | Stale Worker bin missing transitive package after a `dotnet add` | Clean rebuild: `Remove-Item -Recurse -Force src/*/bin, src/*/obj; dotnet build` |
| Web shows empty rooms/parking/carpools | View containers haven't been seeded yet (Worker still starting) | Wait ~30s after "Catalog seeded" log line, then refresh |
| `CosmosException` 404 on `view-*` containers | Web booted before Worker provisioned, and `CosmosWarmupService` didn't run | Check Web's logs for "Cosmos warmup complete" — if missing, restart Web |
| Bumped/orphaned MassTransit receivers ("ReceiveTransport faulted") spamming logs | Worker was orphaned by ungraceful shutdown — it's retrying against dead ports | `Reset-Aspire`, restart |
| `azd up` fails with MFA error | Tenant requires MFA, `azd auth login` didn't run interactively | `azd auth login --tenant <id> --use-device-code` |
| `azd up` says "Cosmos free tier already used" | You used it on another account in this subscription | Either delete the other free-tier account, or accept paid Cosmos (~$24/mo) |
