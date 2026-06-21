namespace Wms.Inbound.Application.Abstractions;

// What: Port (Anti-Corruption Layer; ADR-0011/0014) — akses Product master untuk snapshot uom
// Why: Inbound men-SNAPSHOT uom ke expectedLines GoodsReceipt saat header dibuka (ADR-0014, makna
// dokumen historis stabil). Master diakses via gRPC read-API MasterData (boundary DB-per-service,
// ADR-0011) DI BALIK port core-neutral ini — handler tak tahu gRPC (Hexagonal). ACL: adapter
// menerjemahkan ProductReply asing → ProductSnapshot model Inbound. null = product tak dikenal (handler
// gagalkan GR: tak bisa men-snapshot uom produk asing).
public interface IProductCatalog
{
    Task<ProductSnapshot?> GetProductAsync(string sku, CancellationToken cancellationToken = default);
}

// What: snapshot atribut Product yang dikonsumsi Inbound (ACL model) — hanya uom di scope 04a
// (batchTrackingRequired DEFERRED: belum ada enforcement batch-required saat scan → dead data dihindari)
public sealed record ProductSnapshot(string Uom);
