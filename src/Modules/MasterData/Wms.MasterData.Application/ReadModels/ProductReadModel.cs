namespace Wms.MasterData.Application.ReadModels;

// What: read DTO Product (CQRS read-side; ADR-0004 / ADR-0011) — dipublish read-API gRPC + di-cache
// Why: sisi QUERY membaca langsung ke DTO ringan, bypass aggregate/repository (inti CQRS) — tak
// memuat invariant write-model. record IMMUTABLE → aman di-cache (ICacheStore) tanpa shared mutable
// state. Field = snapshot kritikal (uom/batchTrackingRequired, ADR-0014) + atribut yang dikonsumsi
// core. IsActive TIDAK dibawa: read-API hanya melayani Product aktif (global soft-delete filter).
public sealed record ProductReadModel(
    string Sku,
    string Name,
    string Uom,
    bool BatchTrackingRequired,
    bool ExpiryTrackingRequired,
    bool QcRequiredOnReceipt,
    int? ShelfLifeDays);
