// What: published-language placeholder modul MasterData (read-only authority; ADR-0011)
// Why: MasterData meng-expose read-API gRPC SINKRON (Warehouse/Location/Product) dan TIDAK
// memancarkan integration event di core flow — read-only dari sisi konsumen → tak butuh domain
// event untuk koordinasi (ADR-0011). Project Contracts ini disediakan untuk konsistensi struktural
// blueprint (§3) DAN mereservasi slot event `ProductUpdated` (cache-invalidation) yang
// DICATAT-TAK-DIAKTIFKAN di ADR-0011 amendment — TTL-first tetap default Phase 04a. Saat invalidation
// kelak diaktifkan, `ProductUpdatedV1` (+ const LogicalName, didaftar di asyncapi.yaml & dijaga FF#11)
// lahir di sini tanpa menyentuh struktur. Untuk sekarang: nol tipe published (FF#11 directional aman).
namespace Wms.MasterData.Contracts;
