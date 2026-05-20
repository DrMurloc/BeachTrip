using BeachTrip.Application.Parking;
using MassTransit;

namespace BeachTrip.Application;

// Composable bus registration helpers. Transport and saga repository are
// chosen by the caller — Infrastructure picks ASB + Cosmos; tests pick in-memory.
public static class ApplicationServiceCollectionExtensions
{
    public static IBusRegistrationConfigurator AddBeachTripConsumers(this IBusRegistrationConfigurator bus)
    {
        bus.AddConsumers(typeof(ApplicationServiceCollectionExtensions).Assembly);
        return bus;
    }

    public static ISagaRegistrationConfigurator<ParkingAllocationState> AddParkingAllocationSaga(this IBusRegistrationConfigurator bus)
    {
        return bus.AddSagaStateMachine<ParkingAllocationStateMachine, ParkingAllocationState>();
    }
}
