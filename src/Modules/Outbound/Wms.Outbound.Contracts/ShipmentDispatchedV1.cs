namespace Wms.Outbound.Contracts;

// What: Integration Event (Published Language; ADR-0005 / ADR-0009) — Outbound → Inventory
// Why: saat Wave Ready→Dispatched (overview §C6, truk keluar gudang), Inventory harus menghapus
// semua Stock state Picked yang terikat ke wave — barang sudah fisik keluar. Decoupled dari domain
// Wave; menyeberang broker via Outbox/Inbox.
// How: payload minimal — hanya waveId; Inventory menderivasi Stock mana yang dihapus (semua Picked
// terikat waveId). LogicalName terdaftar di asyncapi.yaml (FF#11). Lahir 03b consumer-first; emitter 03c.
public sealed record ShipmentDispatchedV1(Guid WaveId)
{
    public const string LogicalName = "outbound.shipment_dispatched.v1";
}
