using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Notification.Domain;

namespace Wms.Notification.Persistence.Configurations;

// What: EF Core mapping aggregate NotificationDelivery (DDD persistence; ADR-0010 / ADR-0017)
// Why: memetakan write-model delivery (state machine) ke schema "notification". SubscriptionId nullable
// (delivery DIRECT tanpa subscription). Index (status, queued_at) men-support query worker "pending dulu,
// urut waktu"; index (user_id, channel) men-support inbox in-app WebUI (04e).
// How: HasConversion strongly-typed id (incl. nullable FK logical) + enum→string. POCO murni (FF#2).
public sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("notification_deliveries");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, value => new NotificationDeliveryId(value))
            .ValueGeneratedNever();

        // logical FK ke subscription — nullable (delivery direct tanpa subscription). EF tak menjalankan
        // converter untuk null → kolom null tersimpan apa adanya.
        builder.Property(d => d.SubscriptionId)
            .HasConversion(id => id!.Value, value => new NotificationSubscriptionId(value));

        builder.Property(d => d.UserId).HasMaxLength(128).IsRequired();
        builder.Property(d => d.Channel).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(d => d.EventType).HasMaxLength(128).IsRequired();
        builder.Property(d => d.Title).HasMaxLength(256).IsRequired();
        builder.Property(d => d.Body).HasMaxLength(2048).IsRequired();
        builder.Property(d => d.WarehouseId).HasMaxLength(64);
        builder.Property(d => d.EventRef).HasMaxLength(128).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(d => d.RetryCount).IsRequired();
        builder.Property(d => d.ProviderMessageId).HasMaxLength(128);
        builder.Property(d => d.FailureReason).HasMaxLength(1024);

        builder.HasIndex(d => new { d.Status, d.QueuedAt });
        builder.HasIndex(d => new { d.UserId, d.Channel });
    }
}
