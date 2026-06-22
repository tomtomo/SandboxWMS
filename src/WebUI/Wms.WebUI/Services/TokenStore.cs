namespace Wms.WebUI.Services;

// What: circuit-scoped auth state (Blazor Server — state per SignalR circuit)
// Why: simpan access JWT + identitas selama sesi circuit; di-attach ke tiap request REST ke gateway.
// Thin sandbox: in-memory (tak survive full page reload) — production → cookie/ProtectedSessionStorage
// + authZ enforcement (Phase 07a). Scoped DI = satu instance per circuit, bukan per request HTTP.
public sealed class TokenStore
{
    public string? AccessToken { get; private set; }

    public string? Username { get; private set; }

    public string? UserId { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

    public void SetTokens(string accessToken, string username)
    {
        AccessToken = accessToken;
        Username = username;
    }

    public void SetUserId(string? userId) => UserId = userId;

    public void Clear()
    {
        AccessToken = null;
        Username = null;
        UserId = null;
    }
}
