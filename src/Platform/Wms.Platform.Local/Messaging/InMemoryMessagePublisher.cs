using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Platform.Local.Messaging;

// What: Adapter Local untuk port IMessagePublisher (in-proc pub/sub)
// Why: stand-in broker untuk pengembangan lokal (Aspire) tanpa Service Bus/Pub/Sub —
// adapter konkret yang dipilih saat deploy=Local. Exception per-subscriber diisolasi
// supaya satu handler error tak menggagalkan publish/handler lain: semantik broker =
// "publish sukses = pesan masuk channel", kegagalan konsumer urusan retry/Inbox.
// How: subscriber didaftar via Subscribe() (kapabilitas Local-only, bukan di port);
// PublishAsync fan-out ke semua subscriber, bungkus tiap pemanggilan dengan try/catch.
public sealed class InMemoryMessagePublisher(ILogger<InMemoryMessagePublisher> logger) : IMessagePublisher
{
    private readonly ConcurrentDictionary<Guid, Func<MessageEnvelope, CancellationToken, Task>> _subscribers = new();

    // What: registrasi konsumer in-proc — kembalikan IDisposable untuk unsubscribe
    public IDisposable Subscribe(Func<MessageEnvelope, CancellationToken, Task> handler)
    {
        var id = Guid.NewGuid();
        _subscribers[id] = handler;
        return new Subscription(() => _subscribers.TryRemove(id, out _));
    }

    public async Task PublishAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        foreach (var (_, handler) in _subscribers)
        {
            try
            {
                await handler(envelope, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Subscriber gagal memproses event {EventId} ({LogicalName}).",
                    envelope.EventId, envelope.LogicalName);
            }
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
