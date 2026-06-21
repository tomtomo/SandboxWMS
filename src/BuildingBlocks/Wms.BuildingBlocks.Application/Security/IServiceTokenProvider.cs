namespace Wms.BuildingBlocks.Application.Security;

// What: Port — service-to-service token provider (Hexagonal; ADR-0021 / ADR-0002)
// Why: hop gRPC internal ([ADR-0006]) harus terautentikasi — OAuth2 bearer AUDIENCE-SCOPED per callee,
// diperoleh di balik port core-neutral ini supaya core tak menyeret cloud SDK. Adapter compile-time
// bound: Azure Managed Identity, GCP Service Account OIDC, Local trust-stub (token kosong). Token
// di-mint PLATFORM cloud (bukan auth-svc) & divalidasi offline di callee (ADR-0021).
// How: GetTokenAsync(audience) return bearer untuk callee tertentu; gRPC client interceptor
// menyisipkannya sebagai header Authorization. Local stub return string kosong (tak ada auth di Local).
public interface IServiceTokenProvider
{
    Task<string> GetTokenAsync(string audience, CancellationToken cancellationToken = default);
}
