using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.Notification.Domain;

// What: Strongly-Typed Id (tactical DDD, ADR-0026)
// Why: identitas surrogate NotificationSubscription — cegah id-mixup dgn id aggregate lain.
public sealed record NotificationSubscriptionId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static NotificationSubscriptionId New() => new(Guid.NewGuid());
}
