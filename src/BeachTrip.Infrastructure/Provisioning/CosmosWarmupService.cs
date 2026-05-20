using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BeachTrip.Infrastructure.Provisioning;

// Belt-and-suspenders for the Web process: runs the same CreateIfNotExists
// dance the CatalogSeeder does in Worker, so Web's view-store queries don't
// 404 if it boots before Worker has finished provisioning.
public sealed class CosmosWarmupService : IHostedService
{
    private readonly CosmosClient _cosmos;
    private readonly ILogger<CosmosWarmupService> _log;

    public CosmosWarmupService(CosmosClient cosmos, ILogger<CosmosWarmupService> log)
    {
        _cosmos = cosmos;
        _log = log;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _log.LogInformation("Warming up Cosmos containers...");
        await CosmosProvisioner.EnsureProvisionedAsync(_cosmos, cancellationToken, _log);
        _log.LogInformation("Cosmos warmup complete.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
