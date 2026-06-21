using Wms.BuildingBlocks.Domain.Events;

namespace Wms.BuildingBlocks.Domain.Primitives;

// What: Aggregate Root (DDD)
// Why: satu-satunya entry point konsistensi untuk cluster object-nya; invariant
// ditegakkan di sini, dan HANYA aggregate yang boleh me-raise domain event
// (emission policy ADR-0026) — bukan service/handler.
// How: domain event ditampung di list internal; infrastruktur menariknya saat
// SaveChanges untuk diterjemahkan jadi integration event + Outbox (ADR-0005).
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot(TId id) : base(id) { }

    protected AggregateRoot() { }

    // What: domain-event emission (ADR-0026)
    // Why: dipanggil dari DALAM method aggregate, hanya pada fakta bisnis sukses —
    // tak ada event saat guard gagal.
    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
