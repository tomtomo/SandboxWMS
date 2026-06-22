namespace Wms.Notification.Directory;

// What: Port (ACL; Hexagonal) — resolusi konteks warehouse via MasterData read-API (ADR-0011)
// Why: body notifikasi diperkaya nama warehouse (mis. "GR confirmed di DC Jakarta"); DB-per-service
// melarang baca tabel MasterData langsung → read-API gRPC. WarehouseContext = model SENDIRI (ACL).
// How: GetWarehouseAsync mengembalikan null bila warehouse tak ada (RpcException NotFound → null).
public interface IWarehouseDirectory
{
    Task<WarehouseContext?> GetWarehouseAsync(string warehouseId, CancellationToken cancellationToken = default);
}

// What: model translasi (ACL) warehouse — subset WarehouseReply yang dipakai Notification
public sealed record WarehouseContext(string WarehouseId, string Name);
