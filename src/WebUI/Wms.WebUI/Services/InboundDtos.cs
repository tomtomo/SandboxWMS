using System.ComponentModel.DataAnnotations;

namespace Wms.WebUI.Services;

// What: DTO + form workflow GoodsReceipt (M2). Field bernilai-enum di RESPONSE disimpan STRING
// (JsonDefaults.Web WebUI tak punya JsonStringEnumConverter); enum dipakai hanya di FORM (UI input),
// dikirim ke API sebagai string-name (host Inbound JsonStringEnumConverter).
public enum GoodsReceiptStatus { InProgress = 1, Pending = 2, Confirmed = 3, Hold = 4 }
public enum LineStatus { Good = 1, WrongItem = 2, QcHold = 3 }
public enum DiscrepancyType { ShortDelivery = 1, OverDelivery = 2, WrongItem = 3, QcHold = 4 }
public enum ResolutionAction { AcceptPartial = 1, RejectExcess = 2, ReturnToSupplier = 3, SendToQC = 4 }

public sealed record GoodsReceiptDetailDto(
    Guid GoodsReceiptId, string? PoRef, string? SupplierId, string? DockDoor, string WarehouseId,
    string Status, string? HoldReason,
    IReadOnlyList<ExpectedLineDto> ExpectedLines,
    IReadOnlyList<ScannedLineDto> ScannedLines,
    IReadOnlyList<DiscrepancyDto> Discrepancies);

public sealed record ExpectedLineDto(string Sku, int ExpectedQty, string Uom);
public sealed record ScannedLineDto(string Sku, int ActualQty, string? Batch, DateOnly? Expiry, string LineStatus);
public sealed record DiscrepancyDto(string Sku, string Type, int Qty, string? ResolutionAction, string? ResolutionNote);
// What: read DTO attachment (metadata GRAttachment; ADR-0015). Tidak ada ScanStatus — GRAttachment
// di overview/domain tak memodelkan AV-scan; field fiktif legacy dihapus (backend tak mengirimnya).
public sealed record AttachmentDto(
    Guid Id, string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAt);

public sealed class ScanLineForm
{
    [Required(ErrorMessage = "SKU wajib diisi.")] public string Sku { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "Actual qty harus > 0.")] public int ActualQty { get; set; } = 1;
    public string? Batch { get; set; }
    public DateOnly? Expiry { get; set; }
    [Required(ErrorMessage = "Line Status wajib dipilih.")] public LineStatus LineStatus { get; set; } = LineStatus.Good;
}

public sealed class ResolveDiscrepancyForm
{
    [Required(ErrorMessage = "Action wajib dipilih.")] public ResolutionAction? Action { get; set; }
    [StringLength(512, ErrorMessage = "Note maksimal 512 karakter.")] public string? Note { get; set; }
}

public sealed class HoldGoodsReceiptForm
{
    [Required(ErrorMessage = "Reason wajib diisi.")]
    [StringLength(512, ErrorMessage = "Reason maksimal 512 karakter.")]
    public string Reason { get; set; } = "";
}
