using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Contracts;
using Wms.Notification.Subscriptions;

namespace Wms.Notification.Handlers;

// What: Idempotent event consumer (EIP Idempotent Receiver; ADR-0005/0017) — GRConfirmed → notifikasi
// Why: sisi Notification dari penerimaan barang. SATU event memicu DUA trigger §G: (a) GR ke SPV
// warehouse terkait (proxy gr_confirmed — momen Pending butuh event baru, di-defer); (b) OverDelivery ke
// purchasing+SPV, AKURAT karena rejectedLines reason=RejectExcess persis = excess over-delivery (overview
// §A4). Enqueue + Inbox-mark commit dalam SATU transaksi (Inbox-committed, ADR-0017) → cegah duplikat/hilang
// di partial failure. NOL I/O eksternal di transaksi (resolusi recipient di worker).
// How: cek Inbox (eventId, HandlerType) → enqueuer.Enqueue* (find subscription + Add delivery, NO SaveChanges)
// → MarkProcessed → IUnitOfWork.SaveChanges (commit delivery + Inbox satu tx). occurredAt dari envelope.
public sealed class GoodsReceiptConfirmedNotifier(
    NotificationEnqueuer enqueuer,
    IInboxGuard inbox,
    IUnitOfWork unitOfWork)
{
    // identitas handler untuk composite inbox key (event_id, handler_type) — ADR-0005
    public const string HandlerType = "notification.gr-confirmed";

    // logical event-type INTERNAL untuk subscription over-delivery (purchasing) — bukan broker channel,
    // dipakai sebagai kunci match NotificationSubscription.EventType
    public const string OverDeliveryEventType = "notification.over_delivery";

    public async Task<Result> HandleAsync(
        Guid eventId, DateTimeOffset occurredAt, GRConfirmedV1 message, CancellationToken cancellationToken = default)
    {
        if (await inbox.HasProcessedAsync(eventId, HandlerType, cancellationToken))
            return Result.Success();

        var eventRef = eventId.ToString();

        // Trigger §G: GoodsReceipt → SPV warehouse terkait (default policy in-app + push, di-customize subscription)
        await enqueuer.EnqueueForSubscribersAsync(
            GRConfirmedV1.LogicalName, message.WarehouseId,
            title: "Goods Receipt dikonfirmasi",
            body: $"GoodsReceipt {message.GrId} telah dikonfirmasi.",
            eventRef, occurredAt, cancellationToken);

        // Trigger §G: OverDelivery → purchasing + SPV. rejectedLines reason=RejectExcess = excess over-delivery
        // (overview §A4) → derivasi akurat dari event yang ADA (mechanism-first, tanpa event baru).
        if (message.RejectedLines.Any(line => line.Reason == "RejectExcess"))
            await enqueuer.EnqueueForSubscribersAsync(
                OverDeliveryEventType, message.WarehouseId,
                title: "Over-delivery terdeteksi",
                body: $"GoodsReceipt {message.GrId} memiliki excess over-delivery yang ditolak.",
                eventRef, occurredAt, cancellationToken);

        inbox.MarkProcessed(eventId, HandlerType);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
