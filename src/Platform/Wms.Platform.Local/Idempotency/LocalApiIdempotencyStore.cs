using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Idempotency;

namespace Wms.Platform.Local.Idempotency;

// What: Adapter Local port IApiIdempotencyStore (Postgres api_idempotency)
// Why: implementasi konkret env lokal — baca/tulis tabel infrastructure.api_idempotency di DB service ini.
// Adapter cloud (Redis/Memorystore TTL-native) swap tanpa menyentuh middleware/core (Hexagonal). TTL cleanup
// = job/ops (Local TAK auto-purge — tabel tumbuh sampai job dijalankan; di-flag ADR-0032).
// How: DbContext AMBIENT (scope request middleware). Sengaja tipis: in-flight duplicate (composite PK) /
// gagal-simpan di-tangani best-effort di middleware (response sudah dilayani) — adapter tak parse error DB.
public sealed class LocalApiIdempotencyStore(DbContext db) : IApiIdempotencyStore
{
    public Task<ApiIdempotencyRecord?> TryGetAsync(
        string endpoint, string idempotencyKey, CancellationToken cancellationToken = default)
        => db.Set<ApiIdempotencyRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                record => record.Endpoint == endpoint && record.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public async Task StoreAsync(ApiIdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        db.Set<ApiIdempotencyRecord>().Add(record);
        await db.SaveChangesAsync(cancellationToken);
    }
}
