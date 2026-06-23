using Wms.Notification.Domain;

namespace Wms.Notification.Domain.UnitTests;

// What: behavioral fitness aggregate NotificationSubscription — factory invariant (overview §G, ADR-0019)
// Why: subscription = sumber kebenaran routing notifikasi; invariant (subscriber/eventType/channels non-empty)
// ditegakkan di factory Create via Result (no-throw, FF#7), bukan di handler.
public class NotificationSubscriptionTests
{
    private static readonly NotificationChannel[] OneChannel = [NotificationChannel.InApp];

    [Fact]
    public void Create_with_valid_input_succeeds_active()
    {
        var result = NotificationSubscription.Create(
            NotificationSubscriptionId.New(), SubscriberType.User, "user-1", "inbound.gr_confirmed.v1", OneChannel);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsActive);
        Assert.Equal("user-1", result.Value.SubscriberId);
    }

    [Fact]
    public void Create_without_subscriber_fails()
    {
        var result = NotificationSubscription.Create(
            NotificationSubscriptionId.New(), SubscriberType.User, "  ", "inbound.gr_confirmed.v1", OneChannel);

        Assert.True(result.IsFailure);
        Assert.Equal(NotificationErrors.MissingSubscriber, result.Error);
    }

    [Fact]
    public void Create_without_event_type_fails()
    {
        var result = NotificationSubscription.Create(
            NotificationSubscriptionId.New(), SubscriberType.User, "user-1", "", OneChannel);

        Assert.True(result.IsFailure);
        Assert.Equal(NotificationErrors.MissingEventType, result.Error);
    }

    [Fact]
    public void Create_without_channels_fails()
    {
        var result = NotificationSubscription.Create(
            NotificationSubscriptionId.New(), SubscriberType.User, "user-1", "inbound.gr_confirmed.v1", []);

        Assert.True(result.IsFailure);
        Assert.Equal(NotificationErrors.NoChannels, result.Error);
    }

    [Fact]
    public void Create_dedups_channels()
    {
        var result = NotificationSubscription.Create(
            NotificationSubscriptionId.New(), SubscriberType.User, "user-1", "inbound.gr_confirmed.v1",
            [NotificationChannel.InApp, NotificationChannel.InApp, NotificationChannel.Email]);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Channels.Count);
    }

    [Fact]
    public void Deactivate_sets_inactive()
    {
        var subscription = NotificationSubscription.Create(
            NotificationSubscriptionId.New(), SubscriberType.User, "user-1", "inbound.gr_confirmed.v1", OneChannel).Value;

        subscription.Deactivate();

        Assert.False(subscription.IsActive);
    }
}
