using Wms.BuildingBlocks.Application.Security;

namespace Wms.Platform.Local.Security;

// What: Adapter Local untuk port IServiceTokenProvider (trust-stub; ADR-0021)
// Why: di Local tak ada platform identity (Azure Managed Identity / GCP Service Account OIDC) →
// token KOSONG (trust-stub). Hop gRPC Local tak ter-autentikasi BY DESIGN (ADR-0021); adapter cloud
// (Platform.Azure MI / Platform.Gcp SA OIDC) menggantikan tanpa sentuh core (Hexagonal, FF#1).
// How: GetTokenAsync return string kosong → client credentials skip header Authorization (lihat
// ServiceAuthCallCredentials). Audience diabaikan di Local (relevan saat cloud audience-scoped).
public sealed class LocalServiceTokenProvider : IServiceTokenProvider
{
    public Task<string> GetTokenAsync(string audience, CancellationToken cancellationToken = default)
        => Task.FromResult(string.Empty);
}
