using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace BeachTrip.Infrastructure.Provisioning;

// Defensive CreateIfNotExists for the DB and all containers. The Cosmos emulator can:
// - Be slow to start (HttpRequestException for ~10-30s)
// - Have brief "Collection is not yet available" 404s right after database creation
// Each provisioning call retries with backoff to ride through both.
public static class CosmosProvisioner
{
    public static async Task EnsureProvisionedAsync(CosmosClient client, CancellationToken ct, ILogger? log = null)
    {
        var db = await Retry(
            () => client.CreateDatabaseIfNotExistsAsync(BeachTripCosmosOptions.DatabaseName, cancellationToken: ct),
            $"create database '{BeachTripCosmosOptions.DatabaseName}'",
            ct, log);

        foreach (var (name, partitionKey) in Containers())
        {
            await Retry(
                () => db.Database.CreateContainerIfNotExistsAsync(new ContainerProperties(name, partitionKey), cancellationToken: ct),
                $"create container '{name}'",
                ct, log);
        }
    }

    private static IEnumerable<(string name, string partitionKey)> Containers()
    {
        yield return (BeachTripCosmosOptions.EventsContainer, "/aggregateId");
        yield return (BeachTripCosmosOptions.SagasContainer, "/id");
        yield return (BeachTripCosmosOptions.LeasesContainer, "/id");
        yield return (BeachTripCosmosOptions.Views.Attendees, "/id");
        yield return (BeachTripCosmosOptions.Views.Carpools, "/id");
        yield return (BeachTripCosmosOptions.Views.Rooms, "/id");
        yield return (BeachTripCosmosOptions.Views.ParkingSpots, "/id");
        yield return (BeachTripCosmosOptions.Views.ParkingAllocation, "/id");
    }

    private static async Task<T> Retry<T>(Func<Task<T>> action, string description, CancellationToken ct, ILogger? log)
    {
        var delay = TimeSpan.FromSeconds(2);
        const int maxAttempts = 30;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                log?.LogWarning("Could not {Description} (attempt {Attempt}): {Reason}. Retrying in {Delay}s.",
                    description, attempt, ex.GetType().Name, delay.TotalSeconds);
                await Task.Delay(delay, ct);
                if (delay < TimeSpan.FromSeconds(10))
                    delay += TimeSpan.FromSeconds(2);
            }
        }
    }
}
