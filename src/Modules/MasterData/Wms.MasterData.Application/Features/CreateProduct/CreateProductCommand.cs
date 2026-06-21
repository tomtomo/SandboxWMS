using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.MasterData.Application.Features.CreateProduct;

// What: CQRS Command (ADR-0004) — daftar Product master baru (overview §D)
// Why: write-intent eksplisit menghasilkan Result<string> (sku) sebagai NILAI (no-throw, ADR-0019).
// Tak auditable (mengikuti precedent Create di codebase: CreateGoodsReceipt) — audit difokuskan ke
// operasi sensitif ber-id-diketahui (Deactivate).
public sealed record CreateProductCommand(
    string Sku,
    string Name,
    string Uom,
    bool BatchTrackingRequired,
    bool ExpiryTrackingRequired,
    bool QcRequiredOnReceipt,
    int? ShelfLifeDays) : ICommand<string>;
