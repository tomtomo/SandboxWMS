using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Platform.Local.Messaging;

// What: Adapter Local untuk port IDeadLetterStore (Postgres dead_letter)
// Why: implementasi konkret DLQ untuk environment lokal — pesan racun dipersist ke
// tabel infrastructure.dead_letter di DB service ini. Adapter cloud (Azure/GCP) punya
// implementasi sendiri tanpa menyentuh core (Hexagonal).
// How: tulis lewat DbContext AMBIENT — di-resolve sebagai base DbContext dari DI; modul
// yang memetakan DbContext-nya sendiri ke base DbContext (DB-per-service → satu DbContext
// per host, resolusi tak ambigu). Set<DeadLetterMessage>() valid karena tabelnya
// terpetakan via AddInfrastructureTables di DbContext modul.
public sealed class LocalDeadLetterStore(DbContext db) : IDeadLetterStore
{
    public async Task StoreAsync(DeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        db.Set<DeadLetterMessage>().Add(message);
        await db.SaveChangesAsync(cancellationToken);
    }
}
