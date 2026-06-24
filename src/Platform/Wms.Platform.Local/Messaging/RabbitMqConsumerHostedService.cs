using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Platform.Local.Messaging;

// What: consumer broker RabbitMQ (Event-Driven Consumer; EIP) — hosted service per host (ADR-0029 amendment)
// Why: menjembatani queue RabbitMQ → handler yang di-subscribe host (dispatcher consumer, sudah dibungkus
// ConsumerDeadLetterPipeline). INILAH yang mengaktifkan subscribe-point yang dulu IDLE lintas-proses (ADR-0029).
// How: declare exchange + queue durable per modul, bind "#" (semua event; handler filter LogicalName sendiri).
// AsyncEventingBasicConsumer (DispatchConsumersAsync=true di ConnectionFactory) → handler di-await; prefetch=1
// (sekuensial → ordering + tekanan balik). Sukses → BasicAck. Error transport/deserialize (pipeline consumer
// sudah menelan kegagalan handler → DLQ, jadi praktis tak sampai sini) → BasicNack tanpa requeue (cegah poison
// loop) + log. Producer-only host (tanpa handler) → no-op (tak declare queue).
public sealed class RabbitMqConsumerHostedService(
    IConnection connection,
    RabbitMqConsumer consumer,
    RabbitMqMessagingOptions options,
    ILogger<RabbitMqConsumerHostedService> logger) : IHostedService
{
    private IModel? _channel;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (consumer.Handlers.Count == 0)
        {
            logger.LogInformation("RabbitMQ consumer: tak ada subscriber (producer-only) — queue tak di-declare.");
            return Task.CompletedTask;
        }

        _channel = connection.CreateModel();
        _channel.ExchangeDeclare(options.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        _channel.QueueDeclare(options.QueueName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(options.QueueName, options.ExchangeName, routingKey: "#");
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var rabbitConsumer = new AsyncEventingBasicConsumer(_channel);
        rabbitConsumer.Received += OnReceivedAsync;
        _channel.BasicConsume(options.QueueName, autoAck: false, rabbitConsumer);

        logger.LogInformation(
            "RabbitMQ consumer siap (queue {Queue}, {Count} handler).", options.QueueName, consumer.Handlers.Count);
        return Task.CompletedTask;
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<MessageEnvelope>(ea.Body.Span)
                ?? throw new InvalidOperationException("MessageEnvelope ter-deserialize null.");

            // fan-out ke semua handler; tiap handler (dispatcher) memfilter LogicalName-nya sendiri (return cepat
            // bila bukan miliknya) — identik dgn semantik in-proc fan-out.
            foreach (var handler in consumer.Handlers)
                await handler(envelope, CancellationToken.None);

            _channel!.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "RabbitMQ consume gagal (deliveryTag {Tag}) — nack tanpa requeue (cegah poison loop).", ea.DeliveryTag);
            _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            _channel.Close();
        _channel?.Dispose();
        return Task.CompletedTask;
    }
}
