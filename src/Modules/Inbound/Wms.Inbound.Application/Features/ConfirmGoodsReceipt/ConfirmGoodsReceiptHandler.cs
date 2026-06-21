using System.Text.Json;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Contracts;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Application.Features.ConfirmGoodsReceipt;

// What: CQRS Command Handler (ADR-0004) + domain→integration event translation (ADR-0005)
// Why: konfirmasi GR adalah fakta bisnis yang harus menyeberang ke Inventory. Handler
// menerjemahkan domain event GoodsReceiptConfirmed (in-process) → integration event
// GRConfirmedV1 (published language) lalu menulisnya ke Outbox — bukan publish langsung
// (anti dual-write). Tipe domain tak pernah jadi wire-contract (ADR-0009).
// How: load aggregate → Confirm() (raise domain event) → translate → Enqueue ke outbox
// port → IUnitOfWork commit (state + outbox SATU transaksi).
public sealed class ConfirmGoodsReceiptHandler(
    IGoodsReceiptRepository repository,
    IIntegrationEventOutbox outbox,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> HandleAsync(
        ConfirmGoodsReceiptCommand command, CancellationToken cancellationToken = default)
    {
        var goodsReceipt = await repository.GetAsync(
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

    // What: Message Translator (EIP) — domain event → integration event envelope
    // How: map ke GRConfirmedV1, serialize JSON jadi Payload; EventId baru = identitas
    // outbox/idempotency (consumer dedup via Inbox atas EventId ini).
    private static MessageEnvelope ToEnvelope(GoodsReceiptConfirmed confirmed)
    {
        var payload = new GRConfirmedV1(
            confirmed.GoodsReceiptId.Value,
            confirmed.WarehouseId,
            confirmed.Lines.Select(line => new ReceivedLineV1(line.Sku, line.Quantity)).ToList());

        return new MessageEnvelope(
            EventId: Guid.NewGuid(),
            LogicalName: GRConfirmedV1.LogicalName,
            OccurredAt: DateTimeOffset.UtcNow,
            Payload: JsonSerializer.Serialize(payload),
            Traceparent: null,
            Tracestate: null);
    }
}
