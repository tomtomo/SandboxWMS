using Wms.BuildingBlocks.Application.Abstractions;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Application.Abstractions;

// What: Repository Pattern (DDD) — port GRAttachment (aggregate terpisah, ADR-0015)
// Why: Add/GetById diwarisi dari IRepository — upload bertahap tanpa menyentuh GoodsReceipt. Attachment
// di-Add saat upload (immutable kecuali soft-delete, di-defer). Commit dipisah ke IUnitOfWork.
public interface IGRAttachmentRepository : IRepository<GRAttachment, GRAttachmentId>
{
}
