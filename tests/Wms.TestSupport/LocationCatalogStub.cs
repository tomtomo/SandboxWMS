using Microsoft.Extensions.DependencyInjection;
using Wms.Inventory.Application.Abstractions;

namespace Wms.TestSupport;

// What: test double ILocationCatalog (ACL port MasterData default-location) — Inventory
// Why: Phase 04a follow-up membuat GoodsReceiptConfirmedConsumer bergantung ILocationCatalog (resolve
// receiving/quarantine area via gRPC). Integration test EXISTING tak menguji MasterData — stub
// mengembalikan kode default (REC-01/QC-A, mempertahankan perilaku seed lama) agar consumer bisa
// dikonstruksi & Stock ter-tempat tanpa wiring gRPC nyata. Test DoD GetDefaultLocation pakai reader REAL.
public sealed class InventoryLocationCatalogStub(string receivingArea = "REC-01", string quarantineArea = "QC-A")
    : ILocationCatalog
{
    public Task<string?> GetDefaultLocationCodeAsync(
        string warehouseId, LocationKind kind, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(kind switch
        {
            LocationKind.ReceivingArea => receivingArea,
            LocationKind.QuarantineArea => quarantineArea,
            _ => null,
        });
}

public static class LocationCatalogStubExtensions
{
    public static IServiceCollection AddInventoryLocationCatalogStub(
        this IServiceCollection services, string receivingArea = "REC-01", string quarantineArea = "QC-A")
        => services.AddSingleton<ILocationCatalog>(new InventoryLocationCatalogStub(receivingArea, quarantineArea));
}
