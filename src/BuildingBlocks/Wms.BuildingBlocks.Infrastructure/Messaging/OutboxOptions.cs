namespace Wms.BuildingBlocks.Infrastructure.Messaging;

// What: konfigurasi OutboxDispatcher
// Why: poll interval, ukuran batch, dan batas retry sebelum dead-letter di-ekstrak
// jadi opsi — kalibrasi (Phase 07c) tak perlu menyentuh kode dispatcher.
public sealed class OutboxOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; set; } = 50;

    // batas attempt sebelum pesan dipindah ke Dead Letter Channel
    public int MaxAttempts { get; set; } = 5;
}
