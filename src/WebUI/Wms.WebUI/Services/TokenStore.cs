namespace Wms.WebUI.Services;

// What: circuit-scoped auth state (Blazor Server — state per SignalR circuit).
// Why: simpan access JWT + identitas selama circuit; di-attach ke tiap request REST ke gateway.
// Persist via ProtectedLocalStorage (encrypted) untuk survive reload; di-hydrate sekali per circuit
// (MainLayout.OnAfterRenderAsync). Scoped DI = satu instance per circuit. authZ enforcement → Phase 07a.
public sealed class TokenStore
{
    public string? AccessToken { get; private set; }

    public string? Username { get; private set; }

    public string? UserId { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

    // true setelah hydrate dari storage selesai (sekali per circuit). MainLayout gate @Body pada flag ini.
    public bool Hydrated { get; private set; }

    // di-raise saat state berubah → komponen (MainLayout) subscribe untuk re-render.
    public event Action? OnChange;

    public void SetSession(string accessToken, string username, string? userId)
    {
        AccessToken = accessToken;
        Username = username;
        UserId = userId;
        Notify();
    }

    public void SetUserId(string? userId)
    {
        UserId = userId;
        Notify();
    }

    public void MarkHydrated()
    {
        Hydrated = true;
        Notify();
    }

    public void Clear()
    {
        AccessToken = null;
        Username = null;
        UserId = null;
        Notify();
    }

    private void Notify() => OnChange?.Invoke();
}
