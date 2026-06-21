namespace Wms.BuildingBlocks.Infrastructure.Messaging;

// What: konfigurasi retry consumer sebelum Dead Letter Channel
// Why: batas attempt + jeda backoff di-ekstrak jadi opsi — kalibrasi (Phase 07c, Polly
// split-timeout) tak perlu menyentuh kode pipeline. Default konservatif: 3 attempt,
// jeda 200ms (cukup untuk transient hiccup, tak menahan rail lama untuk poison message).
public sealed class ConsumerRetryOptions
{
    // total attempt (termasuk attempt pertama) sebelum pesan dipindah ke dead_letter
    public int MaxAttempts { get; set; } = 3;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);
}
