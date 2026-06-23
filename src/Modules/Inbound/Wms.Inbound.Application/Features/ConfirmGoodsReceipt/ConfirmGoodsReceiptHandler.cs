using MediatR;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Contracts;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Application.Features.ConfirmGoodsReceipt;

// What: CQRS — Command Handler (MediatR) + domain→integration event translation (ADR-0005)
// Why: konfirmasi GR adalah fakta bisnis yang harus menyeberang ke Inventory. Handler menerjemahkan
// domain event GoodsReceiptConfirmed (in-process, kaya hasil two-axis) → integration event
// GRConfirmedV1 (published language) lalu menulisnya ke Outbox — bukan publish langsung (anti
// dual-write). Tipe domain tak pernah jadi wire-contract (ADR-0009). Result, no-throw (ADR-0019);
// transaksi state+outbox di-commit atomic oleh TransactionBehavior.
// How: load aggregate → Confirm() (enforce invariant + derive received/rejected + raise event) →
// translate → Enqueue ke outbox port → SaveChanges (state + outbox SATU transaksi).
public sealed class ConfirmGoodsReceiptHandler(
    IGoodsReceiptRepository repository,
    IIntegrationEventOutbox outbox,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ConfirmGoodsReceiptCommand, Result>
{
    public async Task<Result> Handle(
        ConfirmGoodsReceiptCommand command, CancellationToken cancellationToken)
    {
        var goodsReceipt = await repository.GetByIdAsync(
            new GoodsReceiptId(command.GoodsReceiptId), cancellationToken);
        if (goodsReceipt is null)
            return Result.Failure(GoodsReceiptErrors.NotFound);

        var result = goodsReceipt.Confirm();
        if (result.IsFailure)
            return result;

        foreach (var confirmed in goodsReceipt.DomainEvents.OfType<GoodsReceiptConfirmed>())
            outbox.Enqueue(ToEnvelope(confirmed));
        goodsReceipt.ClearDomainEvents();

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // What: Message Translator (EIP) — domain event → integration event envelope (ADR-0005)
    // How: map ConfirmedReceived/RejectedLine → ReceivedLineV1/RejectedLineV1; status & reason
    // enum domain → string (ACL boundary, contract serializer-agnostic ADR-0009). EventId baru =
    // identitas outbox/idempotency (consumer dedup via Inbox atas EventId ini).
    private static MessageEnvelope ToEnvelope(GoodsReceiptConfirmed confirmed)
    {
        var payload = new GRConfirmedV1(
            confirmed.GoodsReceiptId.Value,
            confirmed.WarehouseId,
            confirmed.SupplierId,
            confirmed.ReceivedLines
                .Select(line => new ReceivedLineV1(
                    line.Sku, line.Quantity, ToStatus(line.Status), line.Batch, line.Expiry))
                .ToList(),
            confirmed.RejectedLines
                .Select(line => new RejectedLineV1(line.Sku, line.Quantity, ToReason(line.Reason)))
                .ToList());

        return MessageEnvelope.For(GRConfirmedV1.LogicalName, payload);
    }

    // What: ACL enum→wire translation (ADR-0005) — arm default FAIL-LOUD, bukan masking
    // Why: LineStatus/RejectionReason baru yang belum ter-map = crash early (ADR-0019) di translator
    // (.Application) → di-rollback TransactionBehavior, no event di-emit, Outbox tak korup. Masking ke
    // "Good"/"ReturnToSupplier" men-fabrikasi nilai kontrak valid-tapi-salah → OnHand stock korup diam.
    private static string ToStatus(LineStatus status) => status switch
    {
        LineStatus.Good => "Good",
        LineStatus.QcHold => "QcHold",
        LineStatus.WrongItem => "WrongItem",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "LineStatus tak ter-map ke wire")
    };

    private static string ToReason(RejectionReason reason) => reason switch
    {
        RejectionReason.ReturnToSupplier => "ReturnToSupplier",
        RejectionReason.RejectExcess => "RejectExcess",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "RejectionReason tak ter-map ke wire")
    };
}
