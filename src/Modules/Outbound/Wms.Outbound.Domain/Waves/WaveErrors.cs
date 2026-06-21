using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Outbound.Domain;

// What: katalog Error domain Wave (Result pattern, ADR-0019)
// Why: kegagalan bisnis sebagai nilai ber-Code stabil. Input kosong = Validation (400); transisi/agregasi
// ilegal = Conflict (409) — pemetaan otomatis di transport (ADR-0019).
public static class WaveErrors
{
    public static readonly Error NotFound =
        Error.NotFound("wave.not_found", "Wave tidak ditemukan.");

    public static readonly Error NoOrders =
        Error.Validation("wave.no_orders", "wave minimal punya satu order.");

    public static readonly Error NotActive =
        Error.Conflict("wave.not_active", "hanya wave Active yang dapat menerima picking task.");

    // What: gate agregasi Wave→Ready (ADR-0026 aturan agregasi di domain)
    // Why: Ready hanya legal saat SEMUA PickingTask wave sudah Completed; gate ditegakkan domain.
    public static readonly Error NotAllPicked =
        Error.Conflict("wave.not_all_picked", "wave hanya siap saat semua PickingTask Completed.");

    public static readonly Error InvalidDispatch =
        Error.Conflict("wave.invalid_dispatch", "hanya wave Ready yang dapat didispatch.");
}
