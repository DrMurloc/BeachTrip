using BeachTrip.Application.Parking;
using MassTransit;

namespace BeachTrip.Web.Live;

public sealed class SoloDriverBumpedConsumer : IConsumer<SoloDriverBumped>
{
    private readonly LiveUpdates _live;
    public SoloDriverBumpedConsumer(LiveUpdates live) => _live = live;

    public Task Consume(ConsumeContext<SoloDriverBumped> ctx)
    {
        _live.NotifySoloBumped(ctx.Message);
        return Task.CompletedTask;
    }
}
