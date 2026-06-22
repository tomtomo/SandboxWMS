using Wms.BuildingBlocks.Infrastructure.Persistence;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk GRAttachment (DDD; ADR-0015)
// Why: Add/GetById diwarisi dari EfRepository (hapus boilerplate); aggregate terpisah (logical FK ke
// GoodsReceipt) di-Add saja. Commit dipisah ke IUnitOfWork (satu transaksi).
internal sealed class GRAttachmentRepository(InboundDbContext db)
    : EfRepository<GRAttachment, GRAttachmentId, InboundDbContext>(db), IGRAttachmentRepository;
