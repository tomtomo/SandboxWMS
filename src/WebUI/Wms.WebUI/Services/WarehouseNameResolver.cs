namespace Wms.WebUI.Services;

// What: presentation-layer reference-data resolver (id→display lookup) dengan circuit-scoped cache.
// Why: read DTO lintas modul (Inbound GR, Reporting SOH, MasterData Location) hanya membawa WarehouseId,
// bukan Name — bounded context lain tak boleh denormalize nama Warehouse (MasterData-owned). Daripada
// tiap halaman me-resolve ulang (duplikasi + risiko N+1 per-row), satu resolver memuat peta sekali per
// SignalR circuit dan dipakai ulang lintas halaman/navigasi.
// How: Scoped (umur = circuit). EnsureLoadedAsync sekali memanggil ListWarehouses (isActive:null agar
// warehouse non-aktif yang dirujuk row historis tetap ter-resolve), bangun Dictionary Id→Name; Resolve
// O(1) in-memory, fallback ke id mentah supaya cell tak pernah kosong saat id tak dikenal/stale.
public sealed class WarehouseNameResolver(WmsApiClient api)
{
    // pageSize 200: cukup untuk skala sandbox; jika katalog warehouse melampaui ini, peta akan
    // melewatkan entri dan row jatuh ke fallback id (flag untuk endpoint lookup khusus di masa depan).
    private const int MaxWarehouses = 200;

    private IReadOnlyDictionary<Guid, string>? _namesById;

    // idempotent: muat sekali per circuit. Aman dipanggil dari OnInitializedAsync maupun ServerData LoadAsync.
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_namesById is not null)
            return;
        var result = await api.MasterData.ListWarehousesAsync(1, MaxWarehouses, isActive: null, cancellationToken);
        _namesById = result.Success
            ? result.Value!.Items.ToDictionary(w => w.WarehouseId, w => w.Name)
            : new Dictionary<Guid, string>(0);
    }

    public string Resolve(Guid warehouseId)
        => _namesById is not null && _namesById.TryGetValue(warehouseId, out var name)
            ? name
            : warehouseId.ToString();

    // overload untuk DTO yang menyimpan WarehouseId sebagai string GUID (GR list/detail, StockOnHand).
    public string Resolve(string? warehouseId)
        => Guid.TryParse(warehouseId, out var id) ? Resolve(id) : warehouseId ?? string.Empty;
}
