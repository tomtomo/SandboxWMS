using System.Net.Http.Json;

namespace Wms.WebUI.Services;

// What: Adapter REST Notification (ADR-0006/0017) — in-app inbox + mark-as-read via gateway.
public sealed class NotificationApi(IHttpClientFactory httpClientFactory, TokenStore tokenStore)
    : ApiClientBase(httpClientFactory, tokenStore)
{
    public async Task<IReadOnlyList<InAppNotificationRow>> InboxAsync(
        string userId, CancellationToken cancellationToken = default)
        => (await CreateClient().GetFromJsonAsync<PagedResultDto<InAppNotificationRow>>(
               $"/notifications/inbox?userId={Uri.EscapeDataString(userId)}", JsonDefaults.Web, cancellationToken))?.Items ?? [];

    public async Task<ApiResult> MarkReadAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        var response = await CreateClient().PostAsync(
            $"/notifications/deliveries/{deliveryId}/read", content: null, cancellationToken);
        return response.IsSuccessStatusCode
            ? ApiResult.Ok()
            : ApiResult.Fail($"Mark-read gagal ({(int)response.StatusCode}).");
    }
}

// What: read DTO (bentuk respons GET /notifications/inbox) — selaras InAppNotificationRow Notification
public sealed record InAppNotificationRow(
    Guid DeliveryId, string EventType, string Title, string Body,
    string Status, DateTimeOffset QueuedAt, DateTimeOffset? ReadAt);
