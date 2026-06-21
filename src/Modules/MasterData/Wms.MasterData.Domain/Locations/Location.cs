using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.MasterData.Domain;

// What: Aggregate Root (DDD) — Location, authority lokasi fisik di dalam Warehouse (overview §D)
// Why: master referensi tempat Stock berada (receiving/rack/quarantine/staging). Merefer Warehouse
// BY-ID (Vernon IDDD — aggregate merefer aggregate lain via identity, bukan navigation property):
// boundary konsistensi tetap per-aggregate, Warehouse & Location di-transaksikan terpisah. Lifecycle
// isActive soft-delete (ADR-0014). Invariant di factory (no-throw → Result, FF#7).
// How: factory Create memvalidasi warehouseId (non-empty) & code → Result<Location>; Deactivate/
// Activate guard idempotency. type=LocationType menentukan peran lokasi di core flow.
public sealed class Location : AuditableAggregateRoot<LocationId>
{
    // What: referensi Warehouse by-id (Vernon IDDD) — bukan navigation property lintas-aggregate
    public WarehouseId WarehouseId { get; private set; } = null!;

    public LocationType Type { get; private set; }

    public string Code { get; private set; } = null!;

    // What: soft-delete flag (ADR-0014) — false menyembunyikan dari read-API via global query filter
    public bool IsActive { get; private set; }

    private Location() { }

    private Location(LocationId id, WarehouseId warehouseId, LocationType type, string code) : base(id)
    {
        WarehouseId = warehouseId;
        Type = type;
        Code = code;
        IsActive = true;
    }

    // What: factory — location baru (state aktif); invariant warehouseId non-empty & code wajib
    public static Result<Location> Create(LocationId id, WarehouseId warehouseId, LocationType type, string code)
    {
        if (warehouseId is null || warehouseId.Value == Guid.Empty)
            return Result.Failure<Location>(LocationErrors.MissingWarehouse);
        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<Location>(LocationErrors.MissingCode);

        return Result.Success(new Location(id, warehouseId, type, code));
    }

    // What: soft-delete (ADR-0014) — guard cegah double-deactivate
    public Result Deactivate()
    {
        if (!IsActive)
            return Result.Failure(LocationErrors.AlreadyInactive);

        IsActive = false;
        return Result.Success();
    }

    // What: re-aktivasi — guard cegah double-activate
    public Result Activate()
    {
        if (IsActive)
            return Result.Failure(LocationErrors.AlreadyActive);

        IsActive = true;
        return Result.Success();
    }
}
