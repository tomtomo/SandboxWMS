using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Reporting.Persistence;

namespace Wms.Reporting.Rebuild;

// What: rebuild-from-events capability (derived data; ADR-0017) — reset projeksi + Inbox
// Why: karena projection di-DERIVE dari event, ia rebuild-able: saat schema berubah / ada bug, projeksi
// bisa direkonstruksi dengan me-REPLAY event. Reset mengosongkan 4 projeksi DAN Inbox-mark Reporting —
// menghapus dedup supaya event yang di-replay diproses ulang (bukan di-skip Idempotent Receiver). Sumber
// event untuk replay = Outbox retention service produsen (ADR-0017 out-of-scope: event store dedicated).
// How: ExecuteDelete (EF Core 8, set-based DELETE) per tabel. Replay-nya dipicu eksternal (re-publish
// Outbox produsen → rail → dispatcher); di test, stream envelope yang sama diputar ulang.
public sealed class ProjectionRebuilder(ReportingDbContext db)
{
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await db.StockOnHandViews.ExecuteDeleteAsync(cancellationToken);
        await db.ReceivingSummaries.ExecuteDeleteAsync(cancellationToken);
        await db.DispatchSummaries.ExecuteDeleteAsync(cancellationToken);
        await db.OperatorActivities.ExecuteDeleteAsync(cancellationToken);
        // hapus Inbox-mark Reporting → event yang di-replay diproses ulang (bukan di-dedup)
        await db.Set<InboxMessage>().ExecuteDeleteAsync(cancellationToken);
    }
}
