using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.BuildingBlocks.Infrastructure.Messaging;

// What: adapter IInboxGuard berbasis EF Core (DbContext ambient)
// Why: realisasi idempotency consumer memakai helper Inbox yang sama dengan rail
// (HasProcessed/MarkProcessed). MarkProcessed belum SaveChanges — commit oleh
// IUnitOfWork supaya dedup + efek bisnis atomic.
internal sealed class InboxGuard(DbContext db) : IInboxGuard
{
    public Task<bool> HasProcessedAsync(
        Guid eventId, string handlerType, CancellationToken cancellationToken = default)
        => db.HasProcessedAsync(eventId, handlerType, cancellationToken);

    public void MarkProcessed(Guid eventId, string handlerType)
        => db.MarkProcessed(eventId, handlerType, DateTimeOffset.UtcNow);
}
