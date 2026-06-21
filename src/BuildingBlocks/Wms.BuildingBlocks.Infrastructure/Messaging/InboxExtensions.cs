using Microsoft.EntityFrameworkCore;

namespace Wms.BuildingBlocks.Infrastructure.Messaging;

// What: Idempotent Receiver helper (EIP; ADR-0005)
// Why: konsumer wajib aman dijalankan ulang (delivery at-least-once). Helper ini
// dipakai handler integration-event: cek "(event, handler) sudah diproses?" lalu
// tandai. MarkProcessed sengaja TIDAK SaveChanges sendiri — ia di-Add ke DbContext
// yang sama dengan business write supaya keduanya commit dalam SATU transaksi
// (dedup + efek bisnis atomic; tak ada celah "efek jalan tapi belum ter-mark").
// How: extension di DbContext yang menyentuh Set<InboxMessage>() langsung.
public static class InboxExtensions
{
    public static Task<bool> HasProcessedAsync(
        this DbContext db, Guid eventId, string handlerType, CancellationToken ct = default)
        => db.Set<InboxMessage>().AnyAsync(x => x.EventId == eventId && x.HandlerType == handlerType, ct);

    public static void MarkProcessed(
        this DbContext db, Guid eventId, string handlerType, DateTimeOffset processedAt)
        => db.Set<InboxMessage>().Add(new InboxMessage
        {
            EventId = eventId,
            HandlerType = handlerType,
            ProcessedAt = processedAt,
        });
}
