using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.MasterData.Application.Features.DeactivateProduct;

// What: CQRS Command (ADR-0004) + auditable (ADR-0022) — soft-delete Product (set inactive)
// Why: soft-delete menyembunyikan Product dari read-API (operasi sensitif master) → WAJIB ter-audit
// (IAuditableCommand; id diketahui = sku). Hard delete DILARANG (ADR-0014, break referensi historis).
public sealed record DeactivateProductCommand(string Sku) : ICommand, IAuditableCommand
{
    public string AggregateType => "Product";

    public string AggregateId => Sku;
}
