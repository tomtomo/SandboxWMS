using Wms.Inbound.Domain;

namespace Wms.Inbound.Application.Abstractions;

// What: Repository Pattern (DDD) — port di Application, impl EF di Infrastructure
// Why: persistence diisolasi dari use-case; handler bekerja via abstraksi ini, tak
// tahu EF/Postgres (Dependency Inversion, FF#5). Aggregate di-load/-simpan sebagai
// satu kesatuan konsistensi.
// How: Add/Get untuk aggregate GoodsReceipt; commit dipisah ke IUnitOfWork (satu
// transaksi dengan baris Outbox).
public interface IGoodsReceiptRepository
{
    Task AddAsync(GoodsReceipt goodsReceipt, CancellationToken cancellationToken = default);

    Task<GoodsReceipt?> GetAsync(GoodsReceiptId id, CancellationToken cancellationToken = default);
}
