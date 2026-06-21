using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.MasterData.Domain;

// What: Aggregate Root (DDD) — Product, authority katalog SKU (overview §D)
// Why: source-of-truth atribut produk (uom, batchTrackingRequired, expiryTrackingRequired,
// qcRequiredOnReceipt, shelfLifeDays). Field KRITIKAL (uom, batchTrackingRequired) di-SNAPSHOT ke
// aggregate transaksional saat dokumen dibuat (ADR-0014) — read-API + cache MELENGKAPI snapshot,
// bukan menggantikannya. Identity = SKU natural (ProductId membungkus string). isActive soft-delete
// (ADR-0014): hard delete DILARANG (break referensi dokumen historis: GR lama merefer sku).
// How: factory Create memvalidasi sku/name/uom + shelfLifeDays (positif bila diisi) → Result<Product>;
// Deactivate/Activate guard idempotency. IAuditable via AuditableAggregateRoot.
public sealed class Product : AuditableAggregateRoot<ProductId>
{
    public string Name { get; private set; } = null!;

    // What: default Unit of Measure — field KRITIKAL di-snapshot ke OrderLine/ExpectedLine (ADR-0014)
    public string Uom { get; private set; } = null!;

    // What: field KRITIKAL — apakah batch wajib di-capture; di-snapshot ke dokumen (ADR-0014)
    public bool BatchTrackingRequired { get; private set; }

    public bool ExpiryTrackingRequired { get; private set; }

    public bool QcRequiredOnReceipt { get; private set; }

    public int? ShelfLifeDays { get; private set; }

    // What: soft-delete flag (ADR-0014) — false menyembunyikan dari read-API via global query filter
    public bool IsActive { get; private set; }

    private Product() { }

    private Product(
        ProductId id, string name, string uom, bool batchTrackingRequired,
        bool expiryTrackingRequired, bool qcRequiredOnReceipt, int? shelfLifeDays) : base(id)
    {
        Name = name;
        Uom = uom;
        BatchTrackingRequired = batchTrackingRequired;
        ExpiryTrackingRequired = expiryTrackingRequired;
        QcRequiredOnReceipt = qcRequiredOnReceipt;
        ShelfLifeDays = shelfLifeDays;
        IsActive = true;
    }

    // What: factory — product baru (state aktif); invariant sku/name/uom wajib, shelfLifeDays positif
    public static Result<Product> Create(
        string sku, string name, string uom, bool batchTrackingRequired,
        bool expiryTrackingRequired, bool qcRequiredOnReceipt, int? shelfLifeDays)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return Result.Failure<Product>(ProductErrors.MissingSku);
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Product>(ProductErrors.MissingName);
        if (string.IsNullOrWhiteSpace(uom))
            return Result.Failure<Product>(ProductErrors.MissingUom);
        // relational pattern: null (tak di-track) lolos; hanya nilai <= 0 yang gagal
        if (shelfLifeDays is <= 0)
            return Result.Failure<Product>(ProductErrors.InvalidShelfLife);

        return Result.Success(new Product(
            new ProductId(sku), name, uom, batchTrackingRequired,
            expiryTrackingRequired, qcRequiredOnReceipt, shelfLifeDays));
    }

    // What: soft-delete (ADR-0014) — guard cegah double-deactivate
    public Result Deactivate()
    {
        if (!IsActive)
            return Result.Failure(ProductErrors.AlreadyInactive);

        IsActive = false;
        return Result.Success();
    }

    // What: re-aktivasi — guard cegah double-activate
    public Result Activate()
    {
        if (IsActive)
            return Result.Failure(ProductErrors.AlreadyActive);

        IsActive = true;
        return Result.Success();
    }
}
