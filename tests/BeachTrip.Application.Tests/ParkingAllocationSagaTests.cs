using BeachTrip.Application.Parking;
using BeachTrip.Domain;
using BeachTrip.Domain.Attendees;
using BeachTrip.Domain.Parking;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BeachTrip.Application.Tests;

public sealed class ParkingAllocationSagaTests
{
    private static readonly ParkingSpotId Driveway1Id = ParkingSpotId.New();
    private static readonly ParkingSpotId Driveway2Id = ParkingSpotId.New();
    private static readonly ParkingSpotId Street1Id = ParkingSpotId.New();

    private static SpotSeed Driveway1 => new(Driveway1Id, "Driveway-1", ParkingSpotType.Driveway, false, null);
    private static SpotSeed Driveway2 => new(Driveway2Id, "Driveway-2", ParkingSpotType.Driveway, false, null);
    private static SpotSeed Street1 => new(Street1Id, "Street-1", ParkingSpotType.Street, false, null);

    private static async Task<ITestHarness> StartHarness()
    {
        var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<ParkingAllocationStateMachine, ParkingAllocationState>()
                    .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return harness;
    }

    [Fact]
    public async Task Single_carpool_with_seeded_inventory_gets_a_spot()
    {
        var harness = await StartHarness();
        var sagaId = ParkingAllocationStateMachine.SagaId;

        await harness.Bus.Publish(new SeedParkingInventory(sagaId, new[] { Driveway1, Street1 }));
        await harness.Bus.Publish(new CarpoolWantsParking(sagaId, CarpoolId.New(), ParkingPreference.Driveway));

        var allocated = await harness.Published.SelectAsync<ParkingSpotAllocated>().FirstOrDefault();
        Assert.NotNull(allocated);
        Assert.Equal(Driveway1Id, allocated!.Context.Message.SpotId);
    }

    [Fact]
    public async Task Carpool_bumps_solo_from_only_spot()
    {
        var harness = await StartHarness();
        var sagaId = ParkingAllocationStateMachine.SagaId;
        var soloId = AttendeeId.New();
        var carpoolId = CarpoolId.New();

        await harness.Bus.Publish(new SeedParkingInventory(sagaId, new[] { Driveway1 }));
        await harness.Bus.Publish(new SoloDriverWantsParking(sagaId, soloId, ParkingPreference.Driveway));
        await harness.Bus.Publish(new CarpoolWantsParking(sagaId, carpoolId, ParkingPreference.Driveway));

        // Wait for all sagas to process.
        Assert.True(await harness.Consumed.Any<CarpoolWantsParking>());

        var bumped = await harness.Published.SelectAsync<SoloDriverBumped>().FirstOrDefault();
        Assert.NotNull(bumped);
        Assert.Equal(soloId, bumped!.Context.Message.AttendeeId);
    }

    [Fact]
    public async Task Solo_recovers_spot_when_carpool_disbands()
    {
        var harness = await StartHarness();
        var sagaId = ParkingAllocationStateMachine.SagaId;
        var soloId = AttendeeId.New();
        var carpoolId = CarpoolId.New();

        await harness.Bus.Publish(new SeedParkingInventory(sagaId, new[] { Driveway1 }));
        await harness.Bus.Publish(new SoloDriverWantsParking(sagaId, soloId, ParkingPreference.Driveway));
        await harness.Bus.Publish(new CarpoolWantsParking(sagaId, carpoolId, ParkingPreference.Driveway));
        Assert.True(await harness.Consumed.Any<CarpoolWantsParking>());

        await harness.Bus.Publish(new CarpoolReleasesParking(sagaId, carpoolId));
        Assert.True(await harness.Consumed.Any<CarpoolReleasesParking>());

        // After carpool release, the solo should be allocated again.
        var allocations = await harness.Published.SelectAsync<ParkingSpotAllocated>().ToListAsync();
        Assert.Contains(allocations, a =>
            a.Context.Message.SpotId == Driveway1Id &&
            a.Context.Message.Claim is ParkingClaim.Solo solo &&
            solo.AttendeeId == soloId);
    }

    [Fact]
    public async Task Multiple_carpools_prefer_matching_spot_types()
    {
        var harness = await StartHarness();
        var sagaId = ParkingAllocationStateMachine.SagaId;

        var drivewayCarpool = CarpoolId.New();
        var streetCarpool = CarpoolId.New();

        await harness.Bus.Publish(new SeedParkingInventory(sagaId, new[] { Driveway1, Driveway2, Street1 }));
        await harness.Bus.Publish(new CarpoolWantsParking(sagaId, drivewayCarpool, ParkingPreference.Driveway));
        await harness.Bus.Publish(new CarpoolWantsParking(sagaId, streetCarpool, ParkingPreference.Street));

        Assert.True(await harness.Consumed.Any<CarpoolWantsParking>());

        var allocations = await harness.Published.SelectAsync<ParkingSpotAllocated>().ToListAsync();
        var streetAllocation = allocations.Single(a =>
            a.Context.Message.Claim is ParkingClaim.Carpool c && c.CarpoolId == streetCarpool);
        Assert.Equal(Street1Id, streetAllocation.Context.Message.SpotId);
    }
}
