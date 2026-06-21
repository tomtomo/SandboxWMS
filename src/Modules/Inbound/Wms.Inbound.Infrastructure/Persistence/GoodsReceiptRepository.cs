using Microsoft.EntityFrameworkCore;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk GoodsReceipt (DDD; ADR-0010)
// Why: sisi Infrastructure dari port IGoodsReceiptRepository — Application tak tahu EF.
// How: load aggregate utuh (owned lines auto-included EF); Add saja — commit dipisah
// ke IUnitOfWork supaya state + outbox satu transaksi.
internal sealed class GoodsReceiptRepository(InboundDbContext db) : IGoodsReceiptRepository
{
    public Task AddAsync(GoodsReceipt goodsReceipt, CancellationToken cancellationToken = default)
    {
        db.GoodsReceipts.Add(goodsReceipt);
        return Task.CompletedTask;
    }

    public Task<GoodsReceipt?> GetAsync(GoodsReceiptId id, CancellationToken cancellationToken = default)
        => db.GoodsReceipts.FirstOrDefaultAsync(gr => gr.Id == id, cancellationToken);
}
