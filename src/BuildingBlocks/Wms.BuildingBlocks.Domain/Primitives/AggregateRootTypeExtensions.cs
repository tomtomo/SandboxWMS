namespace Wms.BuildingBlocks.Domain.Primitives;

// What: predikat tipe aggregate root (tactical DDD) — apakah <type> turunan AggregateRoot<TId>
// Why: konvensi persistence (mis. optimistic concurrency token ADR-0031) butuh memilih HANYA aggregate
// root — bukan owned/child/value/infra entity — tanpa marker interface non-generik. Satu sumber predikat
// agar konvensi tiap DbContext konsisten (DRY-of-knowledge).
// How: walk base-type chain; true bila ada base yang generic-definition-nya == AggregateRoot<>.
public static class AggregateRootTypeExtensions
{
    public static bool DerivesFromAggregateRoot(this Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AggregateRoot<>))
                return true;
        return false;
    }
}
