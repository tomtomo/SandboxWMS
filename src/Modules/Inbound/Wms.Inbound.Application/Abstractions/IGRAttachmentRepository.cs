using Wms.Inbound.Domain;

namespace Wms.Inbound.Application.Abstractions;

// What: Repository Pattern (DDD) — port GRAttachment di Application, impl EF di Infrastructure
// Why: aggregate terpisah (ADR-0015) dengan repository sendiri — upload bertahap tanpa menyentuh
// GoodsReceipt. Hanya Add untuk scope ini (immutable kecuali soft-delete, di-defer).
public interface IGRAttachmentRepository
{
    Task AddAsync(GRAttachment attachment, CancellationToken cancellationToken = default);
}
