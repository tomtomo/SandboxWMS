namespace Wms.Outbound.Application.Abstractions;

// What: Port (Anti-Corruption Layer; ADR-0011/0005) — akses Product master untuk snapshot uom
// Why: Outbound men-SNAPSHOT uom ke OrderLine saat order masuk (ADR-0014). Master diakses via gRPC
// read-API MasterData (boundary DB-per-service, ADR-0011) DI BALIK port core-neutral ini — handler tak
// tahu gRPC/transport (Hexagonal). ACL: adapter menerjemahkan ProductReply asing → ProductSnapshot
// model Outbound sendiri (tak meminjam tipe MasterData). null = product tak dikenal (handler gagalkan:
// order tak bisa men-snapshot uom produk yang tak ada di katalog).
public interface IProductCatalog
{
    Task<ProductSnapshot?> GetProductAsync(string sku, CancellationToken cancellationToken = default);
}

// What: snapshot atribut Product yang dikonsumsi Outbound (ACL model) — hanya uom di scope 04a
public sealed record ProductSnapshot(string Uom);
