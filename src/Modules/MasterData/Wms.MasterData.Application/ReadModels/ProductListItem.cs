namespace Wms.MasterData.Application.ReadModels;

// What: read DTO list-item Product (CQRS read-side; ADR-0004 / ADR-0011) — dipakai REST list-API (UI)
// Why: SEPARATE dari ProductReadModel (yang merupakan kontrak gRPC by-id, sengaja TANPA IsActive karena
// hanya melayani Product aktif). List-API manajemen butuh IsActive untuk membedakan baris aktif/non-aktif
// (filter & badge di UI), jadi DTO list ini sengaja membawanya — tanpa mengubah kontrak gRPC. record
// immutable → aman lintas layer; Uom/flags tracking di-bawa apa adanya (snapshot ringan, bypass aggregate).
public sealed record ProductListItem(
    string Sku,
    string Name,
    string Uom,
    bool BatchTrackingRequired,
    bool ExpiryTrackingRequired,
    bool QcRequiredOnReceipt,
    int? ShelfLifeDays,
    bool IsActive);
