namespace Wms.BuildingBlocks.Application.Messaging;

// What: Port — message publisher (Hexagonal / Ports & Adapters; ADR-0002)
// Why: Outbox dispatcher (Infrastructure) mem-publish lewat abstraksi ini, bukan
// SDK broker konkret — adapter per-cloud (Local in-proc, Azure Service Bus, GCP
// Pub/Sub) yang memilih implementasi. Inilah seam yang menjaga core nol cloud SDK
// (FF#1) dan membuat "tambah cloud ke-N" cuma menambah adapter.
// How: kontrak minimal — terima MessageEnvelope siap-kirim, kembalikan Task; tak ada
// jejak transport di signature. Idempotency & retry urusan dispatcher + consumer.
public interface IMessagePublisher
{
    Task PublishAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default);
}
