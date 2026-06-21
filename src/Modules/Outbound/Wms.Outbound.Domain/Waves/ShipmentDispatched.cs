using Wms.BuildingBlocks.Domain.Events;

namespace Wms.Outbound.Domain;

// What: Domain Event (DDD; emission policy ADR-0026)
// Why: menandai fakta bisnis "wave didispatch" di dalam Wave aggregate, in-process. Diterjemahkan jadi
// integration event ShipmentDispatchedV1 (published language) di Application sebelum menyeberang broker
// (ADR-0005) → Inventory remove Stock Picked terikat wave (overview §C6). Tipe domain ini tak pernah jadi
// wire-contract (ADR-0009). Single-aggregate fact (hanya waveId) → di-raise aggregate, bukan compose handler.
public sealed record ShipmentDispatched(WaveId WaveId) : IDomainEvent;
