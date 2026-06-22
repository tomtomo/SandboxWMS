using Wms.BuildingBlocks.Infrastructure.Persistence;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk GoodsReceipt (DDD; ADR-0010)
// Why: Add/GetById diwarisi dari EfRepository (hapus boilerplate). GoodsReceipt tak punya query
// write-side lain — read-side dilayani IGoodsReceiptReader (CQRS). Commit dipisah ke IUnitOfWork
// (state + Outbox satu transaksi). Owned lines auto-included EF saat load aggregate.
internal sealed class GoodsReceiptRepository(InboundDbContext db)
    : EfRepository<GoodsReceipt, GoodsReceiptId, InboundDbContext>(db), IGoodsReceiptRepository;
