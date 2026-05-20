using System.Text.Json;
using BeachTrip.Application;
using BeachTrip.Application.Abstractions;
using BeachTrip.Domain;
using BeachTrip.Domain.Attendees;
using BeachTrip.Domain.Carpools;
using BeachTrip.Domain.Parking;
using BeachTrip.Domain.Rooms;
using BeachTrip.Infrastructure.EventStore;
using BeachTrip.Infrastructure.Projections;
using BeachTrip.Infrastructure.Provisioning;
using BeachTrip.Infrastructure.Serialization;
using MassTransit;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BeachTrip.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    // Worker-side: full host with domain consumers, saga, Cosmos repos, projections, ASB.
    public static IHostApplicationBuilder AddBeachTripInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddBeachTripCosmosClient();
        builder.Services.AddBeachTripRepositories();
        builder.Services.AddWorkerMessaging(builder.Configuration);
        builder.Services.AddHostedService<CatalogSeeder>();
        builder.Services.AddHostedService<ProjectionWorker>();
        return builder;
    }

    // Web-side: Cosmos client (for read-model queries) + ASB bus. Caller registers any
    // UI-only consumers via the configureBus callback (e.g. ViewUpdated, SoloDriverBumped).
    public static IHostApplicationBuilder AddBeachTripPublisher(
        this IHostApplicationBuilder builder,
        Action<IBusRegistrationConfigurator>? configureBus = null)
    {
        builder.AddBeachTripCosmosClient();
        builder.Services.AddWebMessaging(builder.Configuration, configureBus);
        return builder;
    }

    public static void AddBeachTripCosmosClient(this IHostApplicationBuilder builder)
    {
        var json = BeachTripJsonOptions.Build();
        builder.Services.AddSingleton(json);
        builder.Services.AddSingleton(_ => DomainEventTypeRegistry.FromDomainAssembly());

        var isEmulator = builder.Environment.IsDevelopment();

        builder.AddAzureCosmosClient("cosmos", configureClientOptions: opts =>
        {
            opts.Serializer = new SystemTextJsonCosmosSerializer(json);

            if (isEmulator)
            {
                opts.ConnectionMode = Microsoft.Azure.Cosmos.ConnectionMode.Gateway;
                opts.LimitToEndpoint = true;
                opts.HttpClientFactory = () => new HttpClient(new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                });
            }
        });
    }

    private static void AddBeachTripRepositories(this IServiceCollection services)
    {
        services.AddSingleton<IRepository<Attendee, AttendeeId>>(sp => BuildRepo<Attendee, AttendeeId>(sp));
        services.AddSingleton<IRepository<Carpool, CarpoolId>>(sp => BuildRepo<Carpool, CarpoolId>(sp));
        services.AddSingleton<IRepository<Room, RoomId>>(sp => BuildRepo<Room, RoomId>(sp));
        services.AddSingleton<IRepository<ParkingSpot, ParkingSpotId>>(sp => BuildRepo<ParkingSpot, ParkingSpotId>(sp));
    }

    private static CosmosEventRepository<TAgg, TId> BuildRepo<TAgg, TId>(IServiceProvider sp)
        where TAgg : Domain.Abstractions.AggregateRoot<TId>
        where TId : notnull
    {
        var client = sp.GetRequiredService<CosmosClient>();
        var container = client.GetContainer(BeachTripCosmosOptions.DatabaseName, BeachTripCosmosOptions.EventsContainer);
        var registry = sp.GetRequiredService<DomainEventTypeRegistry>();
        var json = sp.GetRequiredService<JsonSerializerOptions>();
        return new CosmosEventRepository<TAgg, TId>(container, registry, json);
    }

    private static void AddWorkerMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var asbConnectionString = RequireConnectionString(configuration, "servicebus");

        services.AddMassTransit(bus =>
        {
            bus.AddBeachTripConsumers();
            // Saga uses in-memory storage — MT.Azure.Cosmos@8.5.9's ConfigureEmulator
            // hardcodes localhost:8081, which doesn't match the Aspire-emulator port.
            // Single fixed-ID saga, 4-day app: tolerable. Phase 4-or-later concern.
            bus.AddParkingAllocationSaga().InMemoryRepository();

            bus.UsingAzureServiceBus((ctx, cfg) =>
            {
                cfg.Host(asbConnectionString);
                cfg.ConfigureEndpoints(ctx);
            });
        });
    }

    private static void AddWebMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureBus)
    {
        var asbConnectionString = RequireConnectionString(configuration, "servicebus");

        services.AddMassTransit(bus =>
        {
            configureBus?.Invoke(bus);

            bus.UsingAzureServiceBus((ctx, cfg) =>
            {
                cfg.Host(asbConnectionString);
                cfg.ConfigureEndpoints(ctx);
            });
        });
    }

    private static string RequireConnectionString(IConfiguration configuration, string name) =>
        configuration.GetConnectionString(name)
            ?? throw new InvalidOperationException($"Missing '{name}' connection string. Did AppHost wire the resource?");
}
