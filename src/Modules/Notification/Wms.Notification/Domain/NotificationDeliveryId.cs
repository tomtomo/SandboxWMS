using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.Notification.Domain;

// What: Strongly-Typed Id (tactical DDD, ADR-0026)
// Why: identitas surrogate NotificationDelivery — cegah id-mixup dgn id aggregate lain.
public sealed record NotificationDeliveryId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static NotificationDeliveryId New() => new(Guid.NewGuid());
}
