namespace Wms.Notification.Worker;

// What: konfigurasi worker delivery (mirror OutboxOptions)
// Why: interval poll + batch + batas attempt di-ekstrak jadi opsi — kalibrasi (Phase 07c) tak perlu
// menyentuh kode worker. Default konservatif: poll 1s, batch 20, 3 attempt sebelum Dead Letter Channel
// (selaras ConsumerRetryOptions.MaxAttempts=3).
public sealed class NotificationDeliveryOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    public int BatchSize { get; set; } = 20;

    // total attempt (termasuk attempt pertama) sebelum delivery dipindah ke dead_letter
    public int MaxAttempts { get; set; } = 3;
}
