using System.Net.Http.Json;

namespace Wms.WebUI.Services;

// What: Adapter REST Auth (ADR-0006) — login via gateway → token + userId disimpan di TokenStore
// Why: login = satu-satunya jalur kredensial (REST, bukan gRPC). Setelah token didapat, resolve userId
// (sub) via /auth/me — dipakai inbox notifikasi & membuktikan auth-forward bearer menembus gateway.
public sealed class AuthApiClient(IHttpClientFactory httpClientFactory, TokenStore tokenStore)
    : ApiClientBase(httpClientFactory, tokenStore)
{
    public async Task<string?> LoginAsync(
        string username, string password, CancellationToken cancellationToken = default)
    {
        var response = await CreateClient().PostAsJsonAsync(
            "/auth/login", new { username, password }, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return $"Login gagal ({(int)response.StatusCode}).";

        var tokens = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        if (tokens is null)
            return "Respons login tak terbaca.";

        TokenStore.SetTokens(tokens.AccessToken, username);

        // resolve userId (sub) lewat gateway dgn bearer baru — auth-forward end-to-end
        var me = await CreateClient().GetFromJsonAsync<MeResponse>("/auth/me", cancellationToken);
        TokenStore.SetUserId(me?.UserId);
        return null;
    }

    private sealed record LoginResponse(
        string AccessToken, DateTimeOffset AccessTokenExpiresAt,
        string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

    private sealed record MeResponse(string? UserId);
}
