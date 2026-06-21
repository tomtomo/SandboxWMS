namespace Wms.BuildingBlocks.Domain.Primitives;

// What: Strongly-Typed Id (tactical DDD convention, ADR-0026)
// Why: cegah primitive obsession & id-mixup (mis. lempar WarehouseId ke slot
// ProductId) — compiler menolaknya; default surrogate identity untuk aggregate.
// How: record wrapper di atas nilai primitif; tiap aggregate punya tipe id sendiri
// (mis. `record GoodsReceiptId(Guid Value) : StronglyTypedId<Guid>(Value)`).
public abstract record StronglyTypedId<TValue>(TValue Value)
    where TValue : notnull
{
    public sealed override string ToString() => Value.ToString() ?? string.Empty;
}
