namespace Wms.BuildingBlocks.Application.Messaging;

// What: Port — Idempotent Receiver guard (EIP; ADR-0005)
// Why: consumer integration-event wajib idempotent (delivery at-least-once). Port ini
// mengabstraksi cek + tandai "(event, handler) sudah diproses" tanpa consumer menyentuh
// DbContext/Inbox (Application ⊅ Infrastructure, FF#5). MarkProcessed + business write
// di-commit dalam SATU transaksi oleh IUnitOfWork (tak ada celah efek-tanpa-mark).
// How: composite key (eventId, handlerType) — satu event fan-out ke banyak handler
// di-track independen (multi-consumer safe, ADR-0005).
public interface IInboxGuard
{
    Task<bool> HasProcessedAsync(Guid eventId, string handlerType, CancellationToken cancellationToken = default);

    void MarkProcessed(Guid eventId, string handlerType);
}
