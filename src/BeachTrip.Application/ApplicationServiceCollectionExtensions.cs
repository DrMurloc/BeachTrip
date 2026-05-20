using BeachTrip.Application.Parking;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace BeachTrip.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddBeachTripApplication(this IServiceCollection services)
    {
        services.AddMassTransit(bus =>
        {
            bus.AddConsumers(typeof(ApplicationServiceCollectionExtensions).Assembly);
            bus.AddSagaStateMachine<ParkingAllocationStateMachine, ParkingAllocationState>()
                .InMemoryRepository();
            bus.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
        });

        return services;
    }
}
