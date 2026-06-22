namespace Wms.Notification.Directory;

// What: Port (ACL; Hexagonal) — resolusi recipient detail via Auth read-API (ADR-0011)
// Why: worker butuh email/username untuk dispatch channel Email, tapi DB-per-service (ADR-0010)
// melarang Notification baca tabel Auth langsung → akses lewat read-API gRPC. Port di modul (ACL),
// adapter gRPC di host (di-stub di test). UserContact = model SENDIRI (anti-corruption) — bukan
// UserReply asing.
// How: GetUserAsync mengembalikan null bila user tak ada (RpcException NotFound → null di adapter).
public interface IUserDirectory
{
    Task<UserContact?> GetUserAsync(string userId, CancellationToken cancellationToken = default);
}

// What: model translasi (ACL) recipient — subset UserReply yang dipakai Notification
public sealed record UserContact(string UserId, string Username, string Email);
