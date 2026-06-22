using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Wms.Notification.Domain;

namespace Wms.Notification.Persistence.Configurations;

// What: EF Core mapping aggregate NotificationSubscription (DDD persistence; ADR-0010)
// Why: memetakan aggregate root ke schema "notification" tanpa mencemari domain dgn atribut EF (POCO
// murni, FF#2). Channels (list enum kecil & fixed) dipetakan ke KOLOM TUNGGAL via value-converter CSV —
// hindari tabel owned terpisah untuk set kecil; queryable cukup (filter by eventType/warehouse, bukan channel).
// How: HasConversion strongly-typed id + enum→string; ValueConverter+ValueComparer untuk Channels list.
public sealed class NotificationSubscriptionConfiguration : IEntityTypeConfiguration<NotificationSubscription>
{
    public void Configure(EntityTypeBuilder<NotificationSubscription> builder)
    {
        builder.ToTable("notification_subscriptions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new NotificationSubscriptionId(value))
            .ValueGeneratedNever();

        builder.Property(s => s.SubscriberType).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(s => s.SubscriberId).HasMaxLength(128).IsRequired();
        builder.Property(s => s.EventType).HasMaxLength(128).IsRequired();
        builder.Property(s => s.WarehouseScope).HasMaxLength(64);
        builder.Property(s => s.IsActive).IsRequired();

        // What: Value Converter + Comparer (EF) — list enum ⇄ kolom CSV tunggal
        // Why: Channels = set kecil fixed; CSV menghindari tabel owned. Comparer wajib agar EF deteksi
        // perubahan koleksi (reference-equality default salah untuk mutable collection).
        var channelsConverter = new ValueConverter<IReadOnlyList<NotificationChannel>, string>(
            channels => string.Join(',', channels.Select(channel => channel.ToString())),
            value => value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(Enum.Parse<NotificationChannel>).ToList());
        var channelsComparer = new ValueComparer<IReadOnlyList<NotificationChannel>>(
            (left, right) => left!.SequenceEqual(right!),
            channels => channels.Aggregate(0, (hash, channel) => HashCode.Combine(hash, channel.GetHashCode())),
            channels => channels.ToList());

        builder.Property(s => s.Channels)
            .HasConversion(channelsConverter, channelsComparer)
            .HasMaxLength(64)
            .IsRequired();

        // index pendukung resolusi subscription: by eventType (+ warehouse) saat event tiba
        builder.HasIndex(s => new { s.EventType, s.WarehouseScope });
    }
}
