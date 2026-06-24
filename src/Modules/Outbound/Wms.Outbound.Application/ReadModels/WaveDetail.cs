namespace Wms.Outbound.Application.ReadModels;

// What: read DTO detail (CQRS read-side; ADR-0004) — satu Wave + order yang tergabung + union
// OrderLines lintas order, untuk halaman detail WebUI. Status di-flatten ke string. Lines bukan
// pickingTasks/allocations — melainkan agregasi OrderLine dari order-order wave, tiap line ditandai OrderId.
// How: OrderIds dibaca dari primitive collection (in-memory); Lines dirakit dari follow-up query
// order wave (Id line = indeks 1-based, display "#").
public sealed record WaveDetail(
    Guid WaveId,
    string Status,
    IReadOnlyList<Guid> OrderIds,
    IReadOnlyList<WaveLine> Lines);

// What: satu line gabungan dalam WaveDetail — di-tag dengan OrderId asalnya; Id = indeks 1-based.
public sealed record WaveLine(
    int Id,
    Guid OrderId,
    string Sku,
    int Qty);
