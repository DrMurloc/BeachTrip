using BeachTrip.Infrastructure.Projections;
using MassTransit;

namespace BeachTrip.Web.Live;

public sealed class ViewUpdatedConsumer : IConsumer<ViewUpdated>
{
    private readonly LiveUpdates _live;
    public ViewUpdatedConsumer(LiveUpdates live) => _live = live;

    public Task Consume(ConsumeContext<ViewUpdated> ctx)
    {
        _live.NotifyViewUpdated(ctx.Message);
        return Task.CompletedTask;
    }
}
