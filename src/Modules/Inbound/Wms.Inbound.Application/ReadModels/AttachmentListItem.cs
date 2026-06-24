namespace Wms.Inbound.Application.ReadModels;

// What: read DTO (CQRS read-side; ADR-0004) — metadata satu GRAttachment untuk list UI.
// Why: GRAttachment adalah aggregate terpisah (ADR-0015); endpoint list mengembalikan metadata datar
// (bukan byte) untuk satu GoodsReceipt. ScanStatus SENGAJA tidak ada — GRAttachment tak punya konsep
// scan-status (field itu fiktif di DTO WebUI, akan direkonsiliasi di sisi WebUI).
public sealed record AttachmentListItem(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt);
