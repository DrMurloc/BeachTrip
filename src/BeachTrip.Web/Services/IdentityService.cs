using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace BeachTrip.Web.Services;

public sealed record UserIdentity(Guid AttendeeId, string DisplayName);

// Scoped — one per circuit. Reads/writes ProtectedSessionStorage, which is only
// available after the first render (post-prerender). Pages should fetch via
// OnAfterRenderAsync(firstRender: true) and StateHasChanged when the result lands.
public sealed class IdentityService
{
    private const string Key = "beachtrip-attendee";
    private readonly ProtectedSessionStorage _storage;

    public IdentityService(ProtectedSessionStorage storage) => _storage = storage;

    public async Task<UserIdentity?> GetCurrent()
    {
        var result = await _storage.GetAsync<UserIdentity>(Key);
        return result.Success ? result.Value : null;
    }

    public async Task<UserIdentity> SignIn(string handle, Guid attendeeId)
    {
        var identity = new UserIdentity(attendeeId, handle);
        await _storage.SetAsync(Key, identity);
        return identity;
    }

    public async Task SignOut() => await _storage.DeleteAsync(Key);
}
