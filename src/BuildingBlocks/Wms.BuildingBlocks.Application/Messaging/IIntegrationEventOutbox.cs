namespace Wms.BuildingBlocks.Application.Messaging;

// What: Port — penulisan integration event ke Outbox (Hexagonal; ADR-0005)
// Why: command handler (Application) menulis event ke outbox lewat abstraksi ini —
// TAK menyentuh DbContext/AddToOutbox (Infrastructure) langsung — menjaga dependency
// rule (Application ⊅ Infrastructure, FF#5). Enqueue + state aggregate di-commit dalam
// SATU transaksi oleh IUnitOfWork yang sama (anti dual-write).
// How: impl konkret (Infrastructure) menambah baris OutboxMessage ke DbContext ambient;
// SaveChanges di-trigger terpisah oleh IUnitOfWork.
public interface IIntegrationEventOutbox
{
    void Enqueue(MessageEnvelope envelope);
}
