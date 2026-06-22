using Microsoft.Extensions.Logging;
using Wms.BuildingBlocks.Application.Notification;

namespace Wms.Platform.Local.Notification;

// What: Adapter Local untuk port IPushNotifier (log stub, ADR-0017 channel abstraction)
// Why: lokal tak punya FCM/APNs — "kirim" = log + synthetic providerMessageId. Cloud swap tanpa
// sentuh worker (Hexagonal). Device-registry resolution + branded provider di-defer (out-of-scope 04d).
public sealed class LoggingPushNotifier(ILogger<LoggingPushNotifier> logger) : IPushNotifier
{
    public Task<string> SendAsync(
        string userId, string title, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[push] to={UserId} title={Title} body={Body}", userId, title, body);
        return Task.FromResult($"local-push-{Guid.NewGuid():N}");
    }
}
