using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.Auth.Domain;

// What: Strongly-Typed Id (tactical DDD, ADR-0026) — surrogate identity User
// Why: cegah primitive-obsession & id-mixup antar aggregate Auth (mis. lempar RoleId ke slot
// UserId; compiler menolak). Dirujuk RefreshToken & embedded sebagai claim `sub` di JWT.
public sealed record UserId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static UserId New() => new(Guid.NewGuid());
}
