using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk GRAttachment (DDD; ADR-0015)
// Why: sisi Infrastructure dari port IGRAttachmentRepository — Application tak tahu EF. Aggregate
// terpisah (logical FK ke GoodsReceipt) di-Add saja; commit dipisah ke IUnitOfWork (satu transaksi).
internal sealed class GRAttachmentRepository(InboundDbContext db) : IGRAttachmentRepository
{
    public Task AddAsync(GRAttachment attachment, CancellationToken cancellationToken = default)
    {
        db.Attachments.Add(attachment);
        return Task.CompletedTask;
    }
}
