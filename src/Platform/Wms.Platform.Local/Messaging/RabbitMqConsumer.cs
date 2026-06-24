using System.Collections.Concurrent;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Platform.Local.Messaging;

// What: registry subscriber RabbitMQ (port IMessageSubscriber) — kumpulkan handler dari host (ADR-0029 amendment)
// Why: blok subscribe host SAMA dgn in-proc (subscriber.Subscribe(handler)). Handler sesungguhnya disambung ke
// queue oleh RabbitMqConsumerHostedService saat startup (membaca registry ini). Handler tetap mem-filter
// LogicalName sendiri (lewat dispatcher), jadi binding queue cukup "#" — konsisten dgn fan-out in-proc.
// How: thread-safe; Subscribe kembalikan IDisposable untuk unsubscribe. Registrasi terjadi sinkron sebelum
// app.Run() (Program), jadi handler sudah lengkap saat hosted-service StartAsync membaca Handlers.
public sealed class RabbitMqConsumer : IMessageSubscriber
{
    private readonly ConcurrentDictionary<Guid, Func<MessageEnvelope, CancellationToken, Task>> _handlers = new();

    public IReadOnlyCollection<Func<MessageEnvelope, CancellationToken, Task>> Handlers => _handlers.Values.ToArray();

    public IDisposable Subscribe(Func<MessageEnvelope, CancellationToken, Task> handler)
    {
        var id = Guid.NewGuid();
        _handlers[id] = handler;
        return new Subscription(() => _handlers.TryRemove(id, out _));
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
