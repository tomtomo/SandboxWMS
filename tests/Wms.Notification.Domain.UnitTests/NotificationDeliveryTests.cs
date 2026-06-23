using Wms.Notification.Domain;

namespace Wms.Notification.Domain.UnitTests;

// What: behavioral fitness aggregate NotificationDelivery — state machine Pending→Sent/Failed→Read +
// idempotency (overview §G, ADR-0017/0019). Transisi ilegal & re-delivery ditolak/diserap via Result.
public class NotificationDeliveryTests
{
    private static NotificationDelivery Pending(NotificationChannel channel = NotificationChannel.InApp) =>
        NotificationDelivery.Enqueue(
            NotificationDeliveryId.New(), subscriptionId: null, userId: "user-1", channel,
            eventType: "inbound.gr_confirmed.v1", title: "GR", body: "diterima",
            warehouseId: "WH1", eventRef: Guid.NewGuid().ToString(), queuedAt: DateTimeOffset.UtcNow);

    [Fact]
    public void Enqueue_creates_pending()
    {
        var delivery = Pending();

        Assert.Equal(DeliveryStatus.Pending, delivery.Status);
        Assert.Equal(0, delivery.RetryCount);
    }

    [Fact]
    public void MarkSent_from_pending_sets_sent()
    {
        var delivery = Pending();

        var result = delivery.MarkSent("provider-1", DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
        Assert.Equal("provider-1", delivery.ProviderMessageId);
    }

    [Fact]
    public void MarkSent_from_sent_is_idempotent_noop()
    {
        var delivery = Pending();
        delivery.MarkSent("provider-1", DateTimeOffset.UtcNow);

        var second = delivery.MarkSent("provider-2", DateTimeOffset.UtcNow);

        Assert.True(second.IsSuccess);
        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
        Assert.Equal("provider-1", delivery.ProviderMessageId); // tak ter-overwrite (at-least-once aman)
    }

    [Fact]
    public void MarkFailed_increments_retry_and_sets_failed()
    {
        var delivery = Pending();

        var result = delivery.MarkFailed("smtp timeout");

        Assert.True(result.IsSuccess);
        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal(1, delivery.RetryCount);
    }

    [Fact]
    public void MarkFailed_after_sent_fails()
    {
        var delivery = Pending();
        delivery.MarkSent("provider-1", DateTimeOffset.UtcNow);

        var result = delivery.MarkFailed("late failure");

        Assert.True(result.IsFailure);
        Assert.Equal(NotificationErrors.AlreadySent, result.Error);
    }

    [Fact]
    public void MarkRead_inapp_sent_sets_read()
    {
        var delivery = Pending(NotificationChannel.InApp);
        delivery.MarkSent("provider-1", DateTimeOffset.UtcNow);

        var result = delivery.MarkRead(DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(DeliveryStatus.Read, delivery.Status);
    }

    [Fact]
    public void MarkRead_non_inapp_fails()
    {
        var delivery = Pending(NotificationChannel.Email);
        delivery.MarkSent("provider-1", DateTimeOffset.UtcNow);

        var result = delivery.MarkRead(DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal(NotificationErrors.NotReadable, result.Error);
    }

    [Fact]
    public void MarkRead_when_pending_fails()
    {
        var delivery = Pending(); // belum Sent

        var result = delivery.MarkRead(DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal(NotificationErrors.NotReadable, result.Error);
    }

    [Fact]
    public void HasExhaustedRetries_true_when_retry_reaches_max()
    {
        var delivery = Pending();
        delivery.MarkFailed("1");
        delivery.MarkFailed("2");
        delivery.MarkFailed("3");

        Assert.True(delivery.HasExhaustedRetries(3));
        Assert.False(delivery.HasExhaustedRetries(4));
    }
}
