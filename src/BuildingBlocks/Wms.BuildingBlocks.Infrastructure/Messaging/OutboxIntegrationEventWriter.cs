using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.BuildingBlocks.Infrastructure.Messaging;

// What: adapter IIntegrationEventOutbox — tulis ke tabel outbox via DbContext ambient
// Why: realisasi port penulisan outbox memakai helper AddToOutbox yang sama dengan rail
// (satu definisi mapping outbox). Belum SaveChanges — commit oleh IUnitOfWork supaya
// state + outbox atomic.
internal sealed class OutboxIntegrationEventWriter(DbContext db) : IIntegrationEventOutbox
{
    public void Enqueue(MessageEnvelope envelope) => db.AddToOutbox(envelope);
}
