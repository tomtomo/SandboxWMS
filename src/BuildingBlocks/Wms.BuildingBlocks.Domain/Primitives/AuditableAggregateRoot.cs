using Wms.BuildingBlocks.Domain.Auditing;

namespace Wms.BuildingBlocks.Domain.Primitives;

// What: Aggregate Root ber-audit (DDD seedwork + IAuditable)
// Why: overview (§Konvensi) menetapkan field audit (createdBy/at, modifiedBy/at) UNIVERSAL di
// semua aggregate — disediakan SEKALI di base ini lalu diisi seragam oleh
// AuditableEntityInterceptor, bukan diulang per aggregate. Dipisah dari AggregateRoot polos
// supaya entity non-auditable (mis. tabel infra POCO: outbox/inbox) tak terpaksa berkolom audit.
// How: private setter — HANYA interceptor (via EF change-tracker) yang mengisi; domain tak pernah
// set manual. Audit = fakta infrastruktur (siapa/kapan menyimpan), bukan keputusan domain —
// enkapsulasi terjaga: tak ada public setter yang membuka mutasi audit dari luar.
public abstract class AuditableAggregateRoot<TId> : AggregateRoot<TId>, IAuditable
    where TId : notnull
{
    public string? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset ModifiedAt { get; private set; }

    protected AuditableAggregateRoot(TId id) : base(id) { }

    protected AuditableAggregateRoot() { }
}
