namespace Wms.Inventory.Application;

// What: LOCAL SEED lokasi gudang (placeholder MasterData; overview §D Location)
// Why: Phase 03b butuh kode lokasi (receiving/quarantine area + saran rak putaway) tapi MasterData
// (Warehouse/Location/Product + gRPC read-API) baru lahir di Phase 04a. Sampai itu, lokasi di-SEED
// sebagai konstanta lokal — bukan query master. Saat 04a tiba, ganti seed ini dengan resolusi via
// MasterData read-API (cache-aside) tanpa menyentuh domain.
// How: konstanta string = `code` Location (overview §D: REC-01/QC-A/RACK-*). Putaway strategy
// (closest-empty-bin, ABC, chaotic) = config internal Inventory, OUT-OF-SCOPE di 03b → saran rak
// statis (SuggestedRack). Staging area TIDAK di-seed di sini: dibawa Outbound via PickingCompletedV1.
public static class InventoryLocations
{
    // Stock baru (lineStatus=Good) mendarat di receiving area sebelum putaway
    public const string ReceivingArea = "REC-01";

    // Stock QcHold diisolasi di quarantine area (tak masuk rak reguler)
    public const string QuarantineArea = "QC-A";

    // Saran destinasi putaway (placeholder strategy) — operator override via actualDestination saat Complete
    public const string SuggestedRack = "RACK-A1";
}
