using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Storage;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Application.Features.UploadAttachment;

// What: CQRS — Command Handler (MediatR) — tulis byte ke object storage lalu metadata ke row
// Why: GRAttachment aggregate terpisah (ADR-0015). URUTAN disiplin cegah orphan ROW: factory validasi
// → PutAsync(byte) → repository.Add(row) → SaveChanges (commit oleh TransactionBehavior). Bila byte
// gagal → return sebelum row (tak ada row tanpa byte); bila commit gagal → orphan BLOB (harmless,
// cleanup di-defer ADR-0015). Invariant kunci: tak pernah ada row yang menunjuk blob yang absen.
public sealed class UploadAttachmentHandler(
    IObjectStore objectStore,
    IGRAttachmentRepository repository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UploadAttachmentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UploadAttachmentCommand command, CancellationToken cancellationToken)
    {
        var attachment = GRAttachment.Create(
            GRAttachmentId.New(), command.GoodsReceiptId, command.FileName,
            command.ContentType, command.SizeBytes, DateTimeOffset.UtcNow);
        if (attachment.IsFailure)
            return Result.Failure<Guid>(attachment.Error);

        await objectStore.PutAsync(attachment.Value.BlobPath, command.Content, cancellationToken);
        await repository.AddAsync(attachment.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(attachment.Value.Id.Value);
    }
}
