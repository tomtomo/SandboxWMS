using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Auditing;

namespace Wms.Platform.Local.Auditing;

// What: Adapter Local untuk port IAuditLogStore (Postgres audit_log) — mirror LocalDeadLetterStore
// Why: implementasi konkret audit-log untuk environment lokal — entri ditulis ke tabel
// infrastructure.audit_log di DB service ini. Adapter cloud (Azure/GCP) punya implementasi
// sendiri tanpa menyentuh core (Hexagonal). Out-of-band-ness BUKAN tanggung jawab adapter:
// AuditLogBehavior yang memberinya DbContext SEGAR (scope baru) sehingga tulisan ini lepas dari
// transaksi bisnis yang sudah commit/rollback — adapter cukup persist apa adanya.
// How: tulis lewat DbContext AMBIENT (di scope audit yang baru) → Set<AuditLogEntry>() valid
// karena tabelnya terpetakan via AddInfrastructureTables di DbContext modul (DB-per-service).
public sealed class LocalAuditLogStore(DbContext db) : IAuditLogStore
{
    public async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        db.Set<AuditLogEntry>().Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }
}
