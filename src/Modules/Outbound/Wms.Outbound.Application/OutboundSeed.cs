namespace Wms.Outbound.Application;

// What: LOCAL SEED master data Outbound (placeholder MasterData; overview §D Product, ADR-0014)
// Why: Phase 03c men-snapshot orderLines (sku/qty/uom) saat OutboundOrder masuk, tapi MasterData
// (Product + gRPC read-API) baru lahir di Phase 04a. Sampai itu, `uom` di-SEED sebagai konstanta lokal —
// bukan query Product master. Saat 04a tiba, ganti seed ini dengan resolusi via MasterData read-API
// (cache-aside) tanpa menyentuh domain. Lokasi staging picking dibawa operator via request CompletePicking.
// How: konstanta string = default uom. Snapshot kritikal (ADR-0014: uom dibekukan di OrderLine saat order
// dibuat agar dokumen historis stabil walau Product master kelak berubah).
public static class OutboundSeed
{
    // default Unit of Measure di-snapshot ke OrderLine (stand-in Product.uom sampai 04a)
    public const string DefaultUom = "carton";
}
