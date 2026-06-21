using Microsoft.Extensions.DependencyInjection;

namespace Wms.TestSupport;

// What: test double IProductCatalog (ACL port MasterData read-API) — Inbound & Outbound
// Why: Phase 04a membuat CreateGoodsReceipt/ReceiveOutboundOrder handler bergantung IProductCatalog
// (snapshot uom dari MasterData via gRPC). Integration test EXISTING tak menguji MasterData — cukup
// stub yang mengembalikan uom default ("carton", mempertahankan perilaku seed lama) agar handler bisa
// dikonstruksi & order/GR ter-snapshot tanpa wiring gRPC nyata. Test DoD MasterData (cache miss/hit,
// snapshot via gRPC) memakai adapter + channel REAL, bukan stub ini.
public sealed class InboundProductCatalogStub(string uom = "carton")
    : Wms.Inbound.Application.Abstractions.IProductCatalog
{
    public Task<Wms.Inbound.Application.Abstractions.ProductSnapshot?> GetProductAsync(
        string sku, CancellationToken cancellationToken = default)
        => Task.FromResult<Wms.Inbound.Application.Abstractions.ProductSnapshot?>(new(uom));
}

public sealed class OutboundProductCatalogStub(string uom = "carton")
    : Wms.Outbound.Application.Abstractions.IProductCatalog
{
    public Task<Wms.Outbound.Application.Abstractions.ProductSnapshot?> GetProductAsync(
        string sku, CancellationToken cancellationToken = default)
        => Task.FromResult<Wms.Outbound.Application.Abstractions.ProductSnapshot?>(new(uom));
}

public static class ProductCatalogStubExtensions
{
    public static IServiceCollection AddInboundProductCatalogStub(this IServiceCollection services, string uom = "carton")
        => services.AddSingleton<Wms.Inbound.Application.Abstractions.IProductCatalog>(new InboundProductCatalogStub(uom));

    public static IServiceCollection AddOutboundProductCatalogStub(this IServiceCollection services, string uom = "carton")
        => services.AddSingleton<Wms.Outbound.Application.Abstractions.IProductCatalog>(new OutboundProductCatalogStub(uom));
}
