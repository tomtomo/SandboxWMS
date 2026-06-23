using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.BuildingBlocks.Infrastructure.Messaging;

// What: Transactional Outbox producer helper (ADR-0005)
// Why: produser (command handler) menulis integration event ke outbox lewat helper
// ini DI DALAM transaksi bisnis — bukan publish langsung ke broker (anti dual-write).
// SaveChanges milik produser yang meng-commit state aggregate + baris outbox sekaligus.
// How: map MessageEnvelope → OutboxMessage lalu Add ke DbContext (belum SaveChanges).
public static class OutboxExtensions
{
    // TODO-07B-TRACECONTEXT (ADR-0024): choke point tunggal — saat 07b, capture Activity.Current di sini
    // untuk mengisi envelope.Traceparent/Tracestate (kini null dari producer) → trace utuh menembus hop broker.
    public static void AddToOutbox(this DbContext db, MessageEnvelope envelope)
        => db.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = envelope.EventId,
            LogicalName = envelope.LogicalName,
            Payload = envelope.Payload,
            OccurredAt = envelope.OccurredAt,
            Traceparent = envelope.Traceparent,
            Tracestate = envelope.Tracestate,
        });
}
