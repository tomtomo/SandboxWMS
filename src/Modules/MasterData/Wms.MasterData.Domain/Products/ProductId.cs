using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.MasterData.Domain;

// What: Strongly-Typed Id (tactical DDD, ADR-0026) — NATURAL key = SKU (overview §D: sku PK)
// Why: identitas Product bukan surrogate Guid melainkan SKU (string) — natural key katalog yang
// dirujuk core modul. Tetap strongly-typed untuk cegah primitive-obsession & id-mixup (mis. lempar
// sku ke slot lokasi). Tanpa New() — SKU disuplai pemanggil (bukan di-generate).
public sealed record ProductId(string Value) : StronglyTypedId<string>(Value);
