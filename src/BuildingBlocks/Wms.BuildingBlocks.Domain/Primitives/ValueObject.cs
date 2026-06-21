namespace Wms.BuildingBlocks.Domain.Primitives;

// What: Value Object (DDD)
// Why: kesamaan berbasis NILAI, bukan identity — dua VO dengan komponen sama adalah
// equal; immutable, tanpa lifecycle sendiri.
// How: subclass mengekspos komponen equality via GetEqualityComponents();
// Equals/GetHashCode dihitung dari komponen itu.
public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
            return false;

        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var component in GetEqualityComponents())
            hash.Add(component);
        return hash.ToHashCode();
    }
}
