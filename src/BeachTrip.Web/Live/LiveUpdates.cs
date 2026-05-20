using BeachTrip.Application.Parking;
using BeachTrip.Infrastructure.Projections;

namespace BeachTrip.Web.Live;

// Singleton broadcaster. UI consumers invoke into it; Blazor components subscribe
// to the events in OnInitialized and unsubscribe in Dispose. Events fire on the
// MT consumer thread, so component handlers must marshal via InvokeAsync.
public sealed class LiveUpdates
{
    public event Action<ViewUpdated>? ViewChanged;
    public event Action<SoloDriverBumped>? SoloBumped;

    public void NotifyViewUpdated(ViewUpdated message) => ViewChanged?.Invoke(message);
    public void NotifySoloBumped(SoloDriverBumped message) => SoloBumped?.Invoke(message);
}
