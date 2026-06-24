namespace Wms.BuildingBlocks.Application.Messaging;

// What: Port — message subscriber (Hexagonal / Ports & Adapters; ADR-0002 / ADR-0029 amendment)
// Why: sisi-konsumer kembaran IMessagePublisher. Host menyambung dispatcher consumer ke rail lewat
// abstraksi ini, bukan tipe konkret — sehingga blok subscribe host SAMA baik transport Local in-proc
// (InMemoryMessagePublisher) maupun broker RabbitMQ (RabbitMqConsumer). Adapter dipilih saat composition
// (ada/tidaknya ConnectionStrings:rabbitmq). Inilah subscribe-point yang dulu "IDLE di Local 2-proses"
// (ADR-0029) — kini AKTIF lintas-proses lewat adapter broker.
// How: kontrak minimal — daftarkan handler (MessageEnvelope→Task); kembalikan IDisposable untuk unsubscribe.
// Filter LogicalName + idempotency (Inbox) tetap urusan handler/dispatcher, netral terhadap transport.
public interface IMessageSubscriber
{
    IDisposable Subscribe(Func<MessageEnvelope, CancellationToken, Task> handler);
}
