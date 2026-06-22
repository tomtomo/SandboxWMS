namespace Wms.BuildingBlocks.Application.Notification;

// What: Port (Ports & Adapters / DIP seam) — channel-provider abstraction email
// Why: Notification worker (04d) men-dispatch tanpa terikat provider konkret — Local =
// log stub, cloud = SMTP/SendGrid/ACS swap tanpa menyentuh worker (Hexagonal, ADR-0002).
// Port di BuildingBlocks (bukan modul Notification) supaya adapter Local boleh hidup di
// Platform.Local tanpa melanggar FF#6 (Platform tak me-reference Modules).
// How: SendAsync mengembalikan providerMessageId saat sukses (disimpan di NotificationDelivery);
// kegagalan channel = exception → worker tangani via retry → DLQ (tak boleh propagate ke core).
public interface IEmailSender
{
    Task<string> SendAsync(
        string emailAddress, string subject, string body, CancellationToken cancellationToken = default);
}
