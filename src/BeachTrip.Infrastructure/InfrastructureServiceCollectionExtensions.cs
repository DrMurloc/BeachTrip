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
    // Worker-side: full host with consumers, saga, Cosmos repos, projections, and ASB.
    public static IHostApplicationBuilder AddBeachTripInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddBeachTripCosmosClient();
        builder.Services.AddBeachTripRepositories();
        builder.Services.AddBeachTripMessaging(
            builder.Configuration,
            hostsConsumers: true,
            isEmulator: builder.Environment.IsDevelopment());
        builder.Services.AddHostedService<CatalogSeeder>();
        builder.Services.AddHostedService<ProjectionWorker>();
        return builder;
    }

    // Web-side: publisher + Cosmos client for read-model queries. No consumers run here.
    public static IHostApplicationBuilder AddBeachTripPublisher(this IHostApplicationBuilder builder)
    {
        builder.AddBeachTripCosmosClient();
        builder.Services.AddBeachTripMessaging(
            builder.Configuration,
            hostsConsumers: false,
            isEmulator: builder.Environment.IsDevelopment());
        return builder;
    }

    private static void AddBeachTripCosmosClient(this IHostApplicationBuilder builder)
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
                // Cosmos emulator uses a self-signed cert and only speaks Gateway mode.
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

    private static void AddBeachTripMessaging(this IServiceCollection services, IConfiguration configuration, bool hostsConsumers, bool isEmulator)
    {
        var asbConnectionString = configuration.GetConnectionString("servicebus")
            ?? throw new InvalidOperationException("Missing 'servicebus' connection string. Did AppHost wire the resource?");
        var cosmosConnectionString = configuration.GetConnectionString("cosmos")
            ?? throw new InvalidOperationException("Missing 'cosmos' connection string. Did AppHost wire the resource?");

        services.AddMassTransit(bus =>
        {
            if (hostsConsumers)
            {
                bus.AddBeachTripConsumers();
                // Saga uses in-memory storage for now. MT.Azure.Cosmos's emulator support
                // in 8.5.9 hardcodes the endpoint to https://localhost:8081/ which doesn't
                // match the dynamically-assigned port the Aspire emulator gives us. The saga
                // is a single fixed-ID process manager — losing it on Worker restart is
                // tolerable for a 4-day-lifespan app. Revisit when targeting real Cosmos.
                bus.AddParkingAllocationSaga().InMemoryRepository();
            }

            bus.UsingAzureServiceBus((ctx, cfg) =>
            {
                cfg.Host(asbConnectionString);
                if (hostsConsumers)
                    cfg.ConfigureEndpoints(ctx);
            });
        });
    }
}
