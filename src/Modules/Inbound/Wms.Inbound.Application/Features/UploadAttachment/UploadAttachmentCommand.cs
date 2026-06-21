using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Inbound.Application.Features.UploadAttachment;

// What: CQRS Command (ADR-0004) — upload dokumen pendukung GR (byte → object storage)
// Why: byte TAK masuk DB (ADR-0015); command bawa stream konten + metadata. Content stream
// dikonsumsi sekali oleh handler. BUKAN IAuditableCommand — stream tak cocok di-serialize ke audit.
public sealed record UploadAttachmentCommand(
    Guid GoodsReceiptId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content) : ICommand<Guid>;
