namespace Wms.BuildingBlocks.Application.Notification;

// What: Port (Ports & Adapters / DIP seam) — channel-provider abstraction push
// Why: kembaran IEmailSender untuk channel push — Local = log stub, cloud = FCM/APNs swap
// tanpa sentuh worker. Push di-target ke userId (device-registry resolution di luar scope 04d).
// How: SendAsync kembalikan providerMessageId saat sukses; gagal = exception → retry/DLQ worker.
public interface IPushNotifier
{
    Task<string> SendAsync(
        string userId, string title, string body, CancellationToken cancellationToken = default);
}
