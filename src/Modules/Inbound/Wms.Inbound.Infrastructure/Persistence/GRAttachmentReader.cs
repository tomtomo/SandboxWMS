using Microsoft.EntityFrameworkCore;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Application.ReadModels;

namespace Wms.Inbound.Infrastructure.Persistence;

// What: Read-Port impl EF Core (reader-delegation; ADR-0011) — realisasi IGRAttachmentReader.
// Why: endpoint REST (*.Api) tak menyentuh DbContext (FF#8) — list attachment dilayani di sini, AsNoTracking
// (read murni). Filter atas logical FK GoodsReceiptId (GRAttachment aggregate terpisah, ADR-0015 — tanpa
// navigation property). Bare array (bukan PagedResult): himpunan attachment per GR berbatas-alami.
// How: Where(GoodsReceiptId) → OrderBy UploadedAt → materialize → map metadata datar ke AttachmentListItem.
internal sealed class GRAttachmentReader(InboundDbContext db) : IGRAttachmentReader
{
    public async Task<IReadOnlyList<AttachmentListItem>> ListByGoodsReceiptAsync(
        Guid goodsReceiptId,
        CancellationToken cancellationToken = default)
    {
        var attachments = await db.Attachments
            .AsNoTracking()
            .Where(attachment => attachment.GoodsReceiptId == goodsReceiptId)
            .OrderBy(attachment => attachment.UploadedAt)
            .ToListAsync(cancellationToken);

        return attachments
            .Select(attachment => new AttachmentListItem(
                attachment.Id.Value,
                attachment.FileName,
                attachment.ContentType,
                attachment.SizeBytes,
                attachment.UploadedAt))
            .ToList();
    }
}
