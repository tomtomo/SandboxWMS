using System.ComponentModel.DataAnnotations;

namespace Wms.WebUI.Services;

// What: DTO + form Outbound (M4). Enum 0-based (current; EF persist STRING). RESPONSE field enum = STRING
// (map ke enum via NAMA). Form & payload TIDAK kirim WarehouseId/Uom/ActualQty (stripped vs legacy).
public enum OutboundOrderStatus { New = 0, InProgress = 1, Closed = 2 }
public enum WaveStatus { Active = 0, Ready = 1, Dispatched = 2, Cancelled = 3 }
public enum PickingTaskStatus { Assigned = 0, Completed = 1 }

public sealed record OrderSummaryDto(Guid OrderId, string CustomerId, string ShipTo, string Status, int LineCount, int TotalQty);
public sealed record OrderDetailDto(Guid OrderId, string CustomerId, string ShipTo, string Status, IReadOnlyList<OrderLineDto> Lines);
public sealed record OrderLineDto(int Id, string Sku, int Qty, string Uom);
public sealed record WaveSummaryDto(Guid WaveId, string Status, int OrderCount, int LineCount);
public sealed record WaveDetailDto(Guid WaveId, string Status, IReadOnlyList<Guid> OrderIds, IReadOnlyList<WaveLineDto> Lines);
public sealed record WaveLineDto(int Id, Guid OrderId, string Sku, int Qty);
public sealed record PickingTaskDto(
    Guid PickingTaskId, Guid WaveId, string Sku, string? Batch, int Qty,
    string? AssignedTo, string Status, string? StagingLocationId);

public sealed class CreateOrderForm
{
    [Required(ErrorMessage = "Customer ID wajib diisi.")]
    [StringLength(64, ErrorMessage = "Customer ID maksimal 64 karakter.")]
    public string CustomerId { get; set; } = "";

    [Required(ErrorMessage = "ShipTo wajib diisi.")]
    [StringLength(512, ErrorMessage = "ShipTo maksimal 512 karakter.")]
    public string ShipTo { get; set; } = "";

    public List<OrderLineInput> Lines { get; set; } = new();
}

public sealed class OrderLineInput
{
    [Required(ErrorMessage = "SKU wajib diisi.")]
    public string Sku { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Qty harus > 0.")]
    public int Qty { get; set; } = 1;
}

public sealed class CompletePickingForm
{
    [Required(ErrorMessage = "Staging Location wajib diisi.")]
    public string StagingLocationId { get; set; } = "";
}
