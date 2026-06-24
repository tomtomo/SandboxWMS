using Wms.Inbound.Application.ReadModels;

namespace Wms.Inbound.Application.Abstractions;

// What: Read-Port (CQRS read-side + Ports & Adapters; ADR-0004/0011) — sisi-baca GRAttachment.
// Why: list attachment satu GR mem-bypass aggregate/repository (baca langsung → read-DTO); endpoint REST
// (*.Api) bergantung ke abstraksi ini, BUKAN DbContext (FF#8) — impl EF di Infrastructure. Hasil adalah
// BARE ARRAY (bukan PagedResult): himpunan attachment per GR berbatas-alami (bounded by aggregate scope).
// How: dipanggil ListAttachmentsEndpoint; filter logical FK goodsReceiptId; urut UploadedAt.
public interface IGRAttachmentReader
{
    Task<IReadOnlyList<AttachmentListItem>> ListByGoodsReceiptAsync(
        Guid goodsReceiptId,
        CancellationToken cancellationToken = default);
}
