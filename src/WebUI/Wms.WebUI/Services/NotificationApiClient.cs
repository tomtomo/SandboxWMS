using System.Net.Http.Json;

namespace Wms.WebUI.Services;

// What: Adapter REST Notification (ADR-0006 / ADR-0017) — in-app inbox + mark-as-read via gateway
// Why: inbox kini paginated (PagedResult); UI menampilkan page pertama (.Items). Kontrol paging UI = enhancement.
public sealed class NotificationApiClient(IHttpClientFactory httpClientFactory, TokenStore tokenStore)
    : ApiClientBase(httpClientFactory, tokenStore)
{
    public async Task<IReadOnlyList<InAppNotificationRow>> InboxAsync(
        string userId, CancellationToken cancellationToken = default)
        => (await CreateClient().GetFromJsonAsync<PagedResultDto<InAppNotificationRow>>(
               $"/notifications/inbox?userId={Uri.EscapeDataString(userId)}", cancellationToken))?.Items ?? [];

    public Task MarkReadAsync(Guid deliveryId, CancellationToken cancellationToken = default)
        => CreateClient().PostAsync(
               $"/notifications/deliveries/{deliveryId}/read", content: null, cancellationToken);
}

// What: read DTO (bentuk respons GET /notifications/inbox) — selaras InAppNotificationRow Notification
public sealed record InAppNotificationRow(
    Guid DeliveryId, string EventType, string Title, string Body,
    string Status, DateTimeOffset QueuedAt, DateTimeOffset? ReadAt);
