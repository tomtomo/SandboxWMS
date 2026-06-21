using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.MasterData.Domain;

// What: Aggregate Root (DDD) — Warehouse, authority gudang fisik (overview §D)
// Why: master data referensi untuk core modul (GoodsReceipt/Stock/OutboundOrder melekat ke
// warehouseId). Lifecycle SEDERHANA — bukan state machine kaya seperti core aggregate, hanya flag
// isActive (soft-delete, ADR-0014): hard delete DILARANG karena akan break referential integrity
// dokumen historis. Invariant ditegakkan di factory (no-throw → Result, FF#7); transisi soft-delete
// dijaga guard.
// How: factory Create memvalidasi name/address → Result<Warehouse>; Deactivate/Activate menjaga
// idempotency-guard (tak boleh double-transisi). IAuditable via AuditableAggregateRoot — created_by/
// modified_by diisi interceptor (operator REST CRUD, atau SYSTEM saat seed origin-mesin).
public sealed class Warehouse : AuditableAggregateRoot<WarehouseId>
{
    public string Name { get; private set; } = null!;

    public string Address { get; private set; } = null!;

    // What: soft-delete flag (ADR-0014) — false menyembunyikan dari read-API via global query filter
    public bool IsActive { get; private set; }

    private Warehouse() { }

    private Warehouse(WarehouseId id, string name, string address) : base(id)
    {
        Name = name;
        Address = address;
        IsActive = true;
    }

    // What: factory — warehouse baru (state aktif); invariant name/address wajib
    public static Result<Warehouse> Create(WarehouseId id, string name, string address)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Warehouse>(WarehouseErrors.MissingName);
        if (string.IsNullOrWhiteSpace(address))
            return Result.Failure<Warehouse>(WarehouseErrors.MissingAddress);

        return Result.Success(new Warehouse(id, name, address));
    }

    // What: soft-delete (ADR-0014) — tandai non-aktif; guard cegah double-deactivate
    public Result Deactivate()
    {
        if (!IsActive)
            return Result.Failure(WarehouseErrors.AlreadyInactive);

        IsActive = false;
        return Result.Success();
    }

    // What: re-aktivasi — pulihkan ke aktif; guard cegah double-activate
    public Result Activate()
    {
        if (IsActive)
            return Result.Failure(WarehouseErrors.AlreadyActive);

        IsActive = true;
        return Result.Success();
    }
}
