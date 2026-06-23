using FluentValidation;

namespace Wms.Inbound.Application.Features.ResolveDiscrepancy;

// What: input validation (FluentValidation) — goodsReceiptId/sku wajib non-empty
// Why: input-shape di-fail-fast ValidationBehavior sebelum bisnis; invariant penuh (discrepancy Pending,
// resolusi legal per two-axis) tetap ditegakkan domain (GoodsReceipt.ResolveDiscrepancy) — validator
// melengkapi, bukan menggantikan. Tanpa ini, GoodsReceiptId/Sku kosong lolos ke handler diam-diam.
public sealed class ResolveDiscrepancyValidator : AbstractValidator<ResolveDiscrepancyCommand>
{
    public ResolveDiscrepancyValidator()
    {
        RuleFor(command => command.GoodsReceiptId).NotEmpty();
        RuleFor(command => command.Sku).NotEmpty();
    }
}
