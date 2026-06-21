using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Inbound.Domain;

// What: Aggregate Root (DDD) TERPISAH — GRAttachment (ADR-0015)
// Why: dokumen pendukung GR (ASN/PO/foto) di-upload bertahap & banyak; dijadikan aggregate SENDIRI
// (bukan child GoodsReceipt) supaya upload tak full-load/membengkakkan GR. Tertaut via logical FK
// goodsReceiptId TANPA navigation property — tak menyeret load GoodsReceipt (jaga pemisahan aggregate).
// Byte di object storage; row hanya metadata + blobPath. Tak memancarkan event lintas-modul (silent).
// How: factory Create memvalidasi invariant (whitelist contentType, ≤50MB, fileName) → Result; blobPath
// di-GENERATE berpola {grId}/{attachmentId}/{fileName} → invariant pola terpenuhi by construction.
public sealed class GRAttachment : AuditableAggregateRoot<GRAttachmentId>
{
    private const int MaxFileNameLength = 256;
    private const long MaxSizeBytes = 50L * 1024 * 1024;

    // What: whitelist contentType (ADR-0015) — hanya dokumen/gambar pendukung yang diizinkan
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/jpeg", "image/jpg", "image/png", "image/webp"
    };

    public Guid GoodsReceiptId { get; private set; }

    public string FileName { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public long SizeBytes { get; private set; }

    public string BlobPath { get; private set; } = null!;

    public DateTimeOffset UploadedAt { get; private set; }

    private GRAttachment() { }

    private GRAttachment(
        GRAttachmentId id, Guid goodsReceiptId, string fileName,
        string contentType, long sizeBytes, string blobPath, DateTimeOffset uploadedAt)
        : base(id)
    {
        GoodsReceiptId = goodsReceiptId;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        BlobPath = blobPath;
        UploadedAt = uploadedAt;
    }

    // What: factory + invariant guard (Result pattern, ADR-0019) — satu pintu konstruksi attachment
    public static Result<GRAttachment> Create(
        GRAttachmentId id, Guid goodsReceiptId, string fileName,
        string contentType, long sizeBytes, DateTimeOffset uploadedAt)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Result.Failure<GRAttachment>(GRAttachmentErrors.MissingFileName);
        if (fileName.Length > MaxFileNameLength)
            return Result.Failure<GRAttachment>(GRAttachmentErrors.FileNameTooLong);
        if (!AllowedContentTypes.Contains(contentType))
            return Result.Failure<GRAttachment>(GRAttachmentErrors.ContentTypeNotAllowed);
        if (sizeBytes <= 0)
            return Result.Failure<GRAttachment>(GRAttachmentErrors.NonPositiveSize);
        if (sizeBytes > MaxSizeBytes)
            return Result.Failure<GRAttachment>(GRAttachmentErrors.SizeExceedsLimit);

        var blobPath = $"{goodsReceiptId}/{id.Value}/{fileName}";
        return Result.Success(
            new GRAttachment(id, goodsReceiptId, fileName, contentType, sizeBytes, blobPath, uploadedAt));
    }
}
