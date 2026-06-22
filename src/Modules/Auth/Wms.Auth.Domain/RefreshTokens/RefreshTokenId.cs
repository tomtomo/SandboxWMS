using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.Auth.Domain;

// What: Strongly-Typed Id (tactical DDD, ADR-0026) — surrogate identity RefreshToken
// Why: identitas token rotasi; ReplacedByTokenId memakai tipe yang SAMA untuk merangkai chain
// (rotation chain ADR-0016) — strongly-typed mencegah id-mixup di rantai itu.
public sealed record RefreshTokenId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static RefreshTokenId New() => new(Guid.NewGuid());
}
