using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.BuildingBlocks.Infrastructure.Messaging;

// What: Outbox relay (EIP — Polling Consumer + Guaranteed Delivery; ADR-0005)
// Why: memindahkan event dari tabel outbox ke broker secara async & andal, lepas dari
// transaksi bisnis. Inilah "worker terpisah" yang mewujudkan at-least-once: publish
// gagal → retry di poll berikut; melewati batas → Dead Letter Channel (forensik),
// satu pesan racun tak boleh memblok pesan lain selamanya.
// How: BackgroundService loop tiap PollInterval; DbContext di-resolve PER-SCOPE
// (DbContext scoped, dispatcher singleton). Publish lewat port IMessagePublisher
// (adapter cloud konkret). ProcessOnceAsync dipisah dari loop agar dapat dipicu
// deterministik dari integration test.
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    OutboxOptions options,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sp = scope.ServiceProvider;
                await ProcessOnceAsync(
                    sp.GetRequiredService<DbContext>(),
                    sp.GetRequiredService<IMessagePublisher>(),
                    sp.GetRequiredService<IDeadLetterStore>(),
                    stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // tabel belum di-migrate / DB transient → log & lanjut tick berikutnya
                logger.LogError(ex, "Outbox dispatch tick gagal; retry di interval berikutnya.");
            }

            try
            {
                await Task.Delay(options.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // What: satu pass dispatch (dipakai loop & test)
    // How: ambil batch unsent (ProcessedAt null) urut OccurredAt → publish satu-satu.
    // Sukses → tandai ProcessedAt. Gagal → Attempts++ + LastError; saat Attempts
    // mencapai MaxAttempts → pindah ke dead_letter lalu tandai processed (stop retry).
    // Commit di akhir; sifat at-least-once (duplikat mungkin → konsumer idempotent).
    public async Task<int> ProcessOnceAsync(
        DbContext db,
        IMessagePublisher publisher,
        IDeadLetterStore deadLetters,
        CancellationToken ct = default)
    {
        var batch = await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(options.BatchSize)
            .ToListAsync(ct);

        if (batch.Count == 0)
            return 0;

        var dispatched = 0;
        foreach (var message in batch)
        {
            // rehydrate dari row Outbox persisted (BUKAN MessageEnvelope.For — pertahankan EventId/OccurredAt/
            // Traceparent asli untuk replay-fidelity; For() men-generate identitas baru, salah utuk dispatch ulang).
            var envelope = new MessageEnvelope(
                message.Id, message.LogicalName, message.OccurredAt,
                message.Payload, message.Traceparent, message.Tracestate);

            try
            {
                await publisher.PublishAsync(envelope, ct);
                message.ProcessedAt = DateTimeOffset.UtcNow;
                dispatched++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = Truncate(ex.Message, 2048);
                logger.LogWarning(ex,
                    "Publish gagal: event {EventId} ({LogicalName}), attempt {Attempts}.",
                    message.Id, message.LogicalName, message.Attempts);

                if (message.Attempts >= options.MaxAttempts)
                {
                    await deadLetters.StoreAsync(new DeadLetterMessage
                    {
                        Id = Guid.NewGuid(),
                        EventId = message.Id,
                        LogicalName = message.LogicalName,
                        Payload = message.Payload,
                        Source = $"outbox-dispatch:{message.LogicalName}",
                        Error = message.LastError ?? "unknown",
                        Attempts = message.Attempts,
                        DeadLetteredAt = DateTimeOffset.UtcNow,
                        Traceparent = message.Traceparent,
                        Tracestate = message.Tracestate,
                    }, ct);
                    message.ProcessedAt = DateTimeOffset.UtcNow;  // berhenti retry
                    logger.LogError(
                        "Event {EventId} di-dead-letter setelah {Attempts} attempt.",
                        message.Id, message.Attempts);
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return dispatched;
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
