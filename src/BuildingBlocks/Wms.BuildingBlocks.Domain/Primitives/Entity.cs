namespace Wms.BuildingBlocks.Domain.Primitives;

// What: Entity (DDD)
// Why: identitas — bukan atribut — yang menentukan kesamaan; dua Entity sama jika
// Id-nya sama walau field lain berbeda.
// How: equality berbasis (Type, Id); ctor parameterless protected untuk materialisasi ORM.
public abstract class Entity<TId>
    where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    protected Entity(TId id) => Id = id;

    protected Entity() { }

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other && GetType() == other.GetType() && Id.Equals(other.Id);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
