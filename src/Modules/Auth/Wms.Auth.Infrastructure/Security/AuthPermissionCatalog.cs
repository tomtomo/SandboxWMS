namespace Wms.Auth.Infrastructure.Security;

// What: planning catalog Permission (ADR-0012 deferred authorization) — di-seed, NOT enforced
// Why: mendefinisikan permission `Module.Action` yang AKAN ada saat enforcement diaktifkan (Phase 07a) —
// BUKAN yang aktif sekarang (authZ deferred, ADR-0012). Tumbuh dari core modul (overview §E). Saat
// Wire-Up: grep TODO-AUTH → `[Authorize(Permission=code)]` memakai code dari katalog ini.
public static class AuthPermissionCatalog
{
    // What: (code, description) — sumber seed reference entity Permission (overview §E)
    public static readonly IReadOnlyList<(string Code, string Description)> Permissions = new[]
    {
        ("Inbound.CreateGR", "Buat Goods Receipt header."),
        ("Inbound.ScanItem", "Scan item saat receiving."),
        ("Inbound.PostGR", "Posting / confirm Goods Receipt."),
        ("Inbound.HoldGR", "Hold Goods Receipt."),
        ("Inbound.ResolveDiscrepancy", "Resolve discrepancy GR."),
        ("Inventory.CompletePutaway", "Selesaikan PutawayTask."),
        ("Inventory.AdjustStock", "Adjust stock manual."),
        ("Outbound.CreateWave", "Buat Wave."),
        ("Outbound.CompletePicking", "Selesaikan PickingTask."),
        ("Outbound.DispatchWave", "Dispatch Wave."),
        ("MasterData.ManageProduct", "Kelola master Product."),
        ("MasterData.ManageLocation", "Kelola master Location."),
        ("MasterData.ManageWarehouse", "Kelola master Warehouse."),
        ("Auth.ManageUser", "Kelola User."),
        ("Auth.ManageRole", "Kelola Role."),
        ("Auth.AssignPermission", "Assign Permission ke Role."),
    };
}
