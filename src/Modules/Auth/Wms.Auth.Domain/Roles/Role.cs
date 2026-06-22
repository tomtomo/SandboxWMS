using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Auth.Domain;

// What: Aggregate Root (DDD) — Role, grouping permission RBAC (overview §E, ADR-0012)
// Why: Role mengikat sekumpulan permission code yang di-assign ke User. Saat mint JWT, HANYA role
// AKTIF yang menyumbang permission ke claim self-contained (IsActive filter ADR-0012 — permission dari
// role non-aktif tak boleh bocor ke token). Merefer Permission BY-CODE (natural key), bukan navigation
// property — boundary konsistensi per-aggregate. Soft-delete isActive (ADR-0014). AuthZ enforcement
// tetap DEFERRED (ADR-0012 → Phase 07a); Role mendefinisikan model, bukan penegakan.
// How: PermissionCodes = set di balik List privat (mutasi via AddPermission/RemovePermission idempotent);
// factory + transisi soft-delete return Result (no-throw, FF#7). IAuditable via AuditableAggregateRoot.
public sealed class Role : AuditableAggregateRoot<RoleId>
{
    private readonly List<string> _permissionCodes = new();

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    // What: permission code yang di-include (`Module.Action`) — sumber claim saat mint JWT
    public IReadOnlyCollection<string> PermissionCodes => _permissionCodes.AsReadOnly();

    // What: soft-delete flag (ADR-0014) — false menyembunyikan dari read-API & jalur mint (ADR-0012)
    public bool IsActive { get; private set; }

    private Role() { }

    private Role(RoleId id, string code, string name) : base(id)
    {
        Code = code;
        Name = name;
        IsActive = true;
    }

    // What: factory — role baru (aktif, tanpa permission); invariant code & name wajib
    public static Result<Role> Create(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<Role>(RoleErrors.MissingCode);
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Role>(RoleErrors.MissingName);

        return Result.Success(new Role(new RoleId(Guid.NewGuid()), code, name));
    }

    // What: tambah permission code — IDEMPOTEN (set semantics; duplikat diabaikan)
    public void AddPermission(string permissionCode)
    {
        if (!string.IsNullOrWhiteSpace(permissionCode) && !_permissionCodes.Contains(permissionCode))
            _permissionCodes.Add(permissionCode);
    }

    public void RemovePermission(string permissionCode) => _permissionCodes.Remove(permissionCode);

    // What: soft-delete (ADR-0014) — guard cegah double-deactivate
    public Result Deactivate()
    {
        if (!IsActive)
            return Result.Failure(RoleErrors.AlreadyInactive);

        IsActive = false;
        return Result.Success();
    }

    // What: re-aktivasi — guard cegah double-activate
    public Result Activate()
    {
        if (IsActive)
            return Result.Failure(RoleErrors.AlreadyActive);

        IsActive = true;
        return Result.Success();
    }
}
