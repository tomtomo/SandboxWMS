using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.Auth.Domain;

// What: Strongly-Typed Id (tactical DDD, ADR-0026) — surrogate identity Role
// Why: cegah id-mixup antar aggregate Auth (mis. lempar UserId ke slot RoleId; compiler menolak).
// User merefer Role lewat tipe ini (RoleId collection).
public sealed record RoleId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static RoleId New() => new(Guid.NewGuid());
}
