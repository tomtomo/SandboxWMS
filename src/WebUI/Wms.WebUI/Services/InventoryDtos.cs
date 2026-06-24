using System.ComponentModel.DataAnnotations;

namespace Wms.WebUI.Services;

// What: DTO + form Inventory (M3). Enum 0-based (current domain; EF persist STRING). RESPONSE field
// enum disimpan STRING (map ke enum client-side untuk warna/filter). ID lokasi/warehouse = STRING.
public enum StockStatus { Quarantine = 0, OnHand = 1, Available = 2, Allocated = 3, Picked = 4 }
public enum PutawayTaskStatus { Assigned = 0, Completed = 1 }

public sealed record StockDto(
    Guid StockId, string WarehouseId, string Sku, string LocationId,
    string? Batch, DateOnly? Expiry, int Quantity, string Status,
    Guid SourceGoodsReceiptId, Guid? AllocatedToWaveId, Guid? PickingTaskId);

public sealed record PutawayTaskDto(
    Guid PutawayTaskId, Guid StockId, string SourceLocationId, string SuggestedDestinationId,
    string? ActualDestinationId, string? AssignedTo, string Status);

public sealed class CompletePutawayForm
{
    [Required(ErrorMessage = "Actual Destination wajib diisi.")]
    public string? ActualDestinationId { get; set; }
}

public sealed class AdjustStockForm
{
    [Range(0, int.MaxValue, ErrorMessage = "New Qty harus >= 0.")]
    public int NewQty { get; set; }

    // UI-only audit hint — backend AdjustStockCommand belum punya field reason → TIDAK dikirim (gap P2).
    [StringLength(512, ErrorMessage = "Reason maksimal 512 karakter.")]
    public string? Reason { get; set; }
}
