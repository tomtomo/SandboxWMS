using Microsoft.Extensions.Logging;
using Wms.BuildingBlocks.Application.Notification;

namespace Wms.Platform.Local.Notification;

// What: Adapter Local untuk port IInAppNotifier (log stub, ADR-0017 channel abstraction)
// Why: in-app inbox sudah dipersist sebagai NotificationDelivery (di-query WebUI 04e); adapter Local
// cukup acknowledge "tersedia" + synthetic providerMessageId. Cloud bisa push real-time (SignalR)
// tanpa sentuh worker (Hexagonal).
public sealed class LoggingInAppNotifier(ILogger<LoggingInAppNotifier> logger) : IInAppNotifier
{
    public Task<string> SendAsync(
        string userId, string title, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[in-app] to={UserId} title={Title} body={Body}", userId, title, body);
        return Task.FromResult($"local-inapp-{Guid.NewGuid():N}");
    }
}
