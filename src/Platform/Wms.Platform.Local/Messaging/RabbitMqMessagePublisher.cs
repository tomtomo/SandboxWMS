using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Platform.Local.Messaging;

// What: Adapter broker RabbitMQ untuk port IMessagePublisher (ADR-0029 amendment)
// Why: mewujudkan cross-process delivery NYATA di Local — OutboxDispatcher mem-publish envelope ke topic
// exchange "wms.events" (routing key = LogicalName); consumer per-modul (queue durable) menerimanya.
// Menggantikan in-proc fan-out yang IDLE lintas-proses. Publisher confirm (ConfirmSelect +
// WaitForConfirmsOrDie) → PublishAsync TAK kembali sukses sebelum broker meng-ack, jadi Outbox hanya
// menandai ProcessedAt saat benar-benar terkirim (Guaranteed Delivery, EIP; at-least-once + idempotent receiver).
// How: satu channel publisher (IModel non-thread-safe → lock); exchange di-declare idempoten di ctor.
public sealed class RabbitMqMessagePublisher : IMessagePublisher, IDisposable
{
    private static readonly TimeSpan ConfirmTimeout = TimeSpan.FromSeconds(10);

    private readonly IModel _channel;
    private readonly RabbitMqMessagingOptions _options;
    private readonly object _gate = new();

    public RabbitMqMessagePublisher(
        IConnection connection, RabbitMqMessagingOptions options, ILogger<RabbitMqMessagePublisher> logger)
    {
        _options = options;
        _channel = connection.CreateModel();
        _channel.ExchangeDeclare(options.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        _channel.ConfirmSelect(); // publisher confirms → deteksi gagal-kirim ke broker (bukan fire-and-forget)
        logger.LogInformation("RabbitMQ publisher siap (exchange {Exchange}).", options.ExchangeName);
    }

    public Task PublishAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(envelope);
        lock (_gate)
        {
            var props = _channel.CreateBasicProperties();
            props.Persistent = true;                       // queue durable + msg persistent → survive broker restart
            props.MessageId = envelope.EventId.ToString();
            props.Type = envelope.LogicalName;
            props.ContentType = "application/json";
            _channel.BasicPublish(_options.ExchangeName, routingKey: envelope.LogicalName, basicProperties: props, body: body);
            _channel.WaitForConfirmsOrDie(ConfirmTimeout); // throw bila broker tak meng-ack → Outbox retry tick berikut
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_channel.IsOpen)
            _channel.Close();
        _channel.Dispose();
    }
}
