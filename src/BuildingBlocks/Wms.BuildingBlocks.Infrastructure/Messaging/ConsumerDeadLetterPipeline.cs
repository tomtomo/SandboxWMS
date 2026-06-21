using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.BuildingBlocks.Infrastructure.Messaging;

// What: Decorator (GoF) consumer-side — retry → Dead Letter Channel (EIP; ADR-0005/0010)
// Why: sisi-konsumer kembaran OutboxDispatcher (sisi-produser). Rail Local in-proc TAK
// redeliver (publish gagal di subscriber cuma di-log), jadi retry harus IN-LINE saat
// delivery; pesan racun yang tetap gagal setelah batas attempt dipindah ke store forensik
// (IDeadLetterStore) — tak boleh hilang diam-diam, tak boleh memblok pesan lain selamanya.
// Reusable baseline (Phase 02 harden): Inventory sekarang, Reporting/Notification (Phase 04)
// reuse via Wrap() yang sama. Bukan Polly — manual attempt loop konsisten dgn OutboxDispatcher;
// kalibrasi resilience (circuit breaker/split-timeout) di-defer ke Phase 07c.
// How: Wrap() membungkus handler subscriber (mis. dispatcher.HandleAsync) jadi handler baru.
// Tiap delivery: coba handler; gagal → log + backoff + ulang sampai MaxAttempts; habis →
// tulis DeadLetterMessage lewat IDeadLetterStore (scope baru, karena scope handler sudah
// rollback/dispose) lalu SWALLOW agar rail lanjut. Idempotency tetap urusan consumer (Inbox).
public sealed class ConsumerDeadLetterPipeline(
    IServiceScopeFactory scopeFactory,
    ConsumerRetryOptions options,
    ILogger<ConsumerDeadLetterPipeline> logger)
{
    // What: titik komposisi Decorator — kembalikan handler ber-retry/DLQ dari handler asli.
    // `source` = identitas consumer (mis. handler-type) untuk forensik DeadLetterMessage.Source.
    public Func<MessageEnvelope, CancellationToken, Task> Wrap(
        string source, Func<MessageEnvelope, CancellationToken, Task> handler)
        => (envelope, cancellationToken) => RunAsync(source, handler, envelope, cancellationToken);

    private async Task RunAsync(
        string source,
        Func<MessageEnvelope, CancellationToken, Task> handler,
        MessageEnvelope envelope,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await handler(envelope, cancellationToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "Consumer {Source} gagal event {EventId} ({LogicalName}), attempt {Attempt}/{MaxAttempts}.",
                    source, envelope.EventId, envelope.LogicalName, attempt, options.MaxAttempts);

                if (attempt >= options.MaxAttempts)
                {
                    await DeadLetterAsync(source, envelope, ex, attempt, cancellationToken);
                    return; // poison terparkir di DLQ → swallow, rail lanjut
                }

                try
                {
                    await Task.Delay(options.RetryDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    // What: pindahkan poison message ke Dead Letter Channel (forensik) lewat port
    // How: scope baru → resolve IDeadLetterStore (adapter Platform: Local=tabel dead_letter).
    // EventId = envelope.EventId untuk korelasi; Source membedakan consumer mana yang gagal.
    private async Task DeadLetterAsync(
        string source, MessageEnvelope envelope, Exception ex, int attempts, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var deadLetters = scope.ServiceProvider.GetRequiredService<IDeadLetterStore>();
        await deadLetters.StoreAsync(new DeadLetterMessage
        {
            Id = Guid.NewGuid(),
            EventId = envelope.EventId,
            LogicalName = envelope.LogicalName,
            Payload = envelope.Payload,
            Source = source,
            Error = Truncate(ex.Message, 4096),
            Attempts = attempts,
            DeadLetteredAt = DateTimeOffset.UtcNow,
            Traceparent = envelope.Traceparent,
            Tracestate = envelope.Tracestate,
        }, cancellationToken);

        logger.LogError(
            "Event {EventId} ({LogicalName}) di-dead-letter oleh {Source} setelah {Attempts} attempt.",
            envelope.EventId, envelope.LogicalName, source, attempts);
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
