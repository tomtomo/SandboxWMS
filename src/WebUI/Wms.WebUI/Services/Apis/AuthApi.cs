using System.Net.Http.Json;

namespace Wms.WebUI.Services;

// What: Adapter REST Auth (ADR-0006) — login + resolve userId via gateway. Client murni transport;
// penyimpanan ke TokenStore/ProtectedLocalStorage dilakukan komponen (Login.razor).
public sealed class AuthApi(IHttpClientFactory httpClientFactory, TokenStore tokenStore)
    : ApiClientBase(httpClientFactory, tokenStore)
{
    public async Task<ApiResult<LoginResult>> LoginAsync(
        string username, string password, CancellationToken cancellationToken = default)
    {
        var response = await CreateClient().PostAsJsonAsync(
            "/auth/login", new { username, password }, JsonDefaults.Web, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return ApiResult<LoginResult>.Fail($"Login gagal ({(int)response.StatusCode}).");

        var body = await response.Content.ReadFromJsonAsync<LoginResult>(JsonDefaults.Web, cancellationToken);
        return body is null
            ? ApiResult<LoginResult>.Fail("Respons login tak terbaca.")
            : ApiResult<LoginResult>.Ok(body);
    }

    // resolve userId (sub) lewat gateway dgn bearer aktif (TokenStore) — auth-forward end-to-end
    public async Task<string?> MeAsync(CancellationToken cancellationToken = default)
    {
        var me = await CreateClient().GetFromJsonAsync<MeResponse>(
            "/auth/me", JsonDefaults.Web, cancellationToken);
        return me?.UserId;
    }

    public sealed record LoginResult(
        string AccessToken, DateTimeOffset AccessTokenExpiresAt,
        string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

    private sealed record MeResponse(string? UserId);
}
