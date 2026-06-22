using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Auth.Domain;

// What: Reference Entity (DDD; ADR-0026) — Permission, capability granular `Module.Action` (overview §E)
// Why: SENGAJA bukan Aggregate Root — reference data yang di-SEED (planning catalog, ADR-0012) tanpa
// state machine / domain event / invariant kompleks. Identity NATURAL = code (`Inbound.PostGR`), bukan
// surrogate — dirujuk Role lewat code. AuthZ DEFERRED (ADR-0012): katalog ini mendefinisikan permission
// yang AKAN ada saat enforcement diaktifkan (Phase 07a), bukan yang aktif sekarang.
// How: Entity<string> dengan Id = code; factory hanya validasi code non-empty (Result, no-throw FF#7).
public sealed class Permission : Entity<string>
{
    // What: natural key = code (`Module.Action`) — alias Id untuk kejelasan baca/mapping
    public string Code => Id;

    public string Description { get; private set; } = null!;

    private Permission() { }

    private Permission(string code, string description) : base(code)
    {
        Description = description;
    }

    // What: factory — permission catalog entry; invariant code wajib (natural key)
    public static Result<Permission> Create(string code, string description)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<Permission>(PermissionErrors.MissingCode);

        return Result.Success(new Permission(code, description ?? string.Empty));
    }
}
