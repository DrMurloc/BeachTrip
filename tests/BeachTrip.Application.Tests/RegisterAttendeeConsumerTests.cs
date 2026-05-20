using BeachTrip.Application.Abstractions;
using BeachTrip.Application.Attendees;
using BeachTrip.Domain;
using BeachTrip.Domain.Attendees;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BeachTrip.Application.Tests;

public sealed class RegisterAttendeeConsumerTests
{
    [Fact]
    public async Task Register_persists_attendee_to_repository()
    {
        var repo = new InMemoryRepository<Attendee, AttendeeId>();

        await using var provider = new ServiceCollection()
            .AddSingleton<IRepository<Attendee, AttendeeId>>(repo)
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<RegisterAttendeeConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var attendeeId = AttendeeId.New();
        await harness.Bus.Publish(new RegisterAttendee(attendeeId, "DrMurloc"));

        Assert.True(await harness.Consumed.Any<RegisterAttendee>());
        var consumer = harness.GetConsumerHarness<RegisterAttendeeConsumer>();
        Assert.True(await consumer.Consumed.Any<RegisterAttendee>());

        var attendee = await repo.Get(attendeeId);
        Assert.NotNull(attendee);
        Assert.Equal("DrMurloc", attendee!.DisplayName);
    }
}
