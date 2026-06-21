using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Inbound.Domain;

// What: katalog Error factory GRAttachment (Result pattern, ADR-0019)
// Why: invariant upload (ADR-0015) ditolak sebagai NILAI ber-Code stabil, bukan exception.
public static class GRAttachmentErrors
{
    public static readonly Error MissingFileName =
        Error.Validation("gr_attachment.missing_file_name", "fileName wajib diisi.");

    public static readonly Error FileNameTooLong =
        Error.Validation("gr_attachment.file_name_too_long", "fileName maksimal 256 karakter.");

    public static readonly Error ContentTypeNotAllowed =
        Error.Validation("gr_attachment.content_type_not_allowed", "contentType di luar whitelist (pdf/jpeg/jpg/png/webp).");

    public static readonly Error NonPositiveSize =
        Error.Validation("gr_attachment.non_positive_size", "sizeBytes harus lebih dari nol.");

    public static readonly Error SizeExceedsLimit =
        Error.Validation("gr_attachment.size_exceeds_limit", "sizeBytes melebihi batas 50MB.");
}
