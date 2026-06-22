using Wms.BuildingBlocks.Application.Abstractions;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Application.Abstractions;

// What: Repository Pattern (DDD) — port write-side GoodsReceipt, impl EF di Infrastructure
// Why: persistence diisolasi dari use-case (DIP, FF#5); Add/GetById diwarisi dari IRepository (hapus
// duplikasi), commit dipisah ke IUnitOfWork. Aggregate di-load/-simpan sebagai satu kesatuan konsistensi.
// Tak ada query tambahan — read-side (list) dilayani IGoodsReceiptReader (CQRS read-side bypass).
public interface IGoodsReceiptRepository : IRepository<GoodsReceipt, GoodsReceiptId>
{
}
