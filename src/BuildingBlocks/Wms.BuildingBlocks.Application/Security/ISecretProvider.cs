namespace Wms.BuildingBlocks.Application.Security;

// What: Port (Hexagonal / Ports & Adapters; ADR-0002 named ports) — secret provider
// Why: material rahasia (mis. RS256 private signing key, ADR-0016) tak boleh hard-coded di source
// atau menyeret cloud SDK ke core — diambil di balik port core-neutral ini. Adapter compile-time bound
// per environment: Azure Key Vault, GCP Secret Manager (branded Phase 05/06/07), Local = config-backed.
// Auth-svc menandatangani JWT dengan private key dari port ini; verifikasi pakai PUBLIC key (didistribusi
// offline, BUKAN rahasia) → hot-path validasi tak menyentuh port ini. → Canon: ADR-0002 (Hexagonal);
// ADR-0016 (RS256 signing key via ISecretProvider); MS Learn Key Vault / Google Secret Manager.
// How: GetSecretAsync(name) mengembalikan nilai rahasia by logical name; null = tak ditemukan
// (caller fail-secure: key kosong → fail-fast saat startup, bukan diam-diam terbitkan token tak ber-sign).
public interface ISecretProvider
{
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);
}
