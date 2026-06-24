namespace Wms.Platform.Local.Messaging;

// What: opsi adapter RabbitMQ (ADR-0029 amendment) — nama exchange bersama + queue durable per host.
// Why: exchange topic tunggal "wms.events" (routing key = LogicalName) jadi titik fan-out; tiap modul
// consumer punya queue durable sendiri (true pub/sub fan-out). Producer-only host: QueueName kosong → tak
// declare queue (lihat RabbitMqConsumerHostedService).
public sealed class RabbitMqMessagingOptions
{
    public string ExchangeName { get; set; } = "wms.events";

    public string QueueName { get; set; } = "";
}
