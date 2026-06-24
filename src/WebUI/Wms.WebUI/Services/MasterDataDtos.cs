using System.ComponentModel.DataAnnotations;

namespace Wms.WebUI.Services;

// What: read DTO + form model MasterData (selaras read model backend + CreateXxxRequest).
// LocationDto.Type = STRING enum-name (backend mengirim Type.ToString(); JsonDefaults.Web tanpa
// JsonStringEnumConverter tak bisa deserialize enum dari string). Enum LocationType tetap dipakai di
// FORM input + FILTER + param request (dikirim sbg string-name saat create/list; backend Enum.TryParse).
public sealed record ProductDto(
    string Sku, string Name, string Uom,
    bool BatchTrackingRequired, bool ExpiryTrackingRequired, bool QcRequiredOnReceipt,
    int? ShelfLifeDays, bool IsActive);

public sealed record WarehouseDto(Guid WarehouseId, string Name, string Address, bool IsActive);

public sealed record LocationDto(Guid LocationId, Guid WarehouseId, string Type, string Code, bool IsActive);

public enum LocationType
{
    ReceivingArea = 1,
    Rack = 2,
    QuarantineArea = 3,
    StagingArea = 4,
}

public sealed class ProductForm
{
    [Required(ErrorMessage = "SKU wajib diisi.")]
    [StringLength(64, ErrorMessage = "SKU maksimal 64 karakter.")]
    public string Sku { get; set; } = "";

    [Required(ErrorMessage = "Name wajib diisi.")]
    [StringLength(256, ErrorMessage = "Name maksimal 256 karakter.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "UOM wajib diisi.")]
    [StringLength(16, ErrorMessage = "UOM maksimal 16 karakter.")]
    public string Uom { get; set; } = "";

    public bool BatchTrackingRequired { get; set; }
    public bool ExpiryTrackingRequired { get; set; }
    public bool QcRequiredOnReceipt { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Shelf Life Days harus > 0.")]
    public int? ShelfLifeDays { get; set; }
}

public sealed class WarehouseForm
{
    [Required(ErrorMessage = "Name wajib diisi.")]
    [StringLength(128, ErrorMessage = "Name maksimal 128 karakter.")]
    public string Name { get; set; } = "";

    [StringLength(512, ErrorMessage = "Address maksimal 512 karakter.")]
    public string Address { get; set; } = "";
}

public sealed class LocationForm
{
    // M1: dropdown warehouse tak bisa di-isi (warehouse-list = gap) → input Warehouse Id GUID manual.
    [Required(ErrorMessage = "Warehouse Id wajib diisi.")]
    public Guid? WarehouseId { get; set; }

    [Required(ErrorMessage = "Type wajib dipilih.")]
    public LocationType? Type { get; set; }

    [Required(ErrorMessage = "Code wajib diisi.")]
    [StringLength(64, ErrorMessage = "Code maksimal 64 karakter.")]
    public string Code { get; set; } = "";
}
