namespace Wms.BuildingBlocks.Application.Notification;

// What: Port (Ports & Adapters / DIP seam) — channel-provider abstraction in-app
// Why: in-app = satu-satunya channel yang persistensi inbox-nya ada di Notification sendiri
// (NotificationDelivery, di-query WebUI 04e + mark-as-read). Adapter Local cukup acknowledge
// (delivery sudah tersimpan); cloud bisa push real-time (SignalR/WebSocket) tanpa sentuh worker.
// How: SendAsync kembalikan providerMessageId saat sukses; gagal = exception → retry/DLQ worker.
public interface IInAppNotifier
{
    Task<string> SendAsync(
        string userId, string title, string body, CancellationToken cancellationToken = default);
}
