namespace Wms.BuildingBlocks.Domain.Events;

// What: Domain Event (DDD)
// Why: menandai fakta bisnis yang sudah terjadi di dalam aggregate. Bersifat
// in-process pada transaksi aggregate, lalu DITERJEMAHKAN jadi integration event
// ber-versi (ADR-0005). Tipe Domain ini tak pernah jadi wire-contract.
public interface IDomainEvent
{
}
