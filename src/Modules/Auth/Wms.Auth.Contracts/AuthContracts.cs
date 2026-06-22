// What: published-language placeholder modul Auth (read-only authority; ADR-0011)
// Why: Auth meng-expose read-API gRPC SINKRON (User/Role/Permission) dan TIDAK memancarkan integration
// event di core flow — read-only dari sisi konsumen → tak butuh domain event untuk koordinasi (ADR-0011).
// Project Contracts disediakan untuk konsistensi struktural blueprint (§3). Untuk sekarang: nol tipe
// published (FF#11 directional aman). Bila kelak Auth perlu memberi sinyal (mis. UserDisabled untuk
// invalidasi sesi lintas-service), event ber-versi lahir di sini + didaftar di asyncapi.yaml (FF#11).
namespace Wms.Auth.Contracts;
