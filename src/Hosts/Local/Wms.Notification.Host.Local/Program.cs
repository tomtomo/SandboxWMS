using System.Text.Json.Serialization;
using Wms.Auth.Grpc;
using Wms.BuildingBlocks.Infrastructure.DependencyInjection;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.BuildingBlocks.Infrastructure.Resilience;
using Wms.BuildingBlocks.Web.Correlation;
using Wms.BuildingBlocks.Web.Grpc;
using Wms.MasterData.Grpc;
using Wms.Notification.DependencyInjection;
using Wms.Notification.Directory;
using Wms.Notification.Endpoints;
using Wms.Notification.Handlers;
using Wms.Notification.Messaging;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.Platform.Hosting;
using Wms.Platform.Local.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// service defaults agnostic: health + service discovery + HTTP resilience + OTel (ADR-0008)
builder.AddServiceDefaults();

// Notification = PURE CONSUMER (ADR-0017): consume 2 event lintas-context → subscription → enqueue
// NotificationDelivery + worker async dispatch ke channel (idempotency + retry→DLQ). DbContext
// (notification + inbox/dead_letter) + adapter Local (IDeadLetterStore + channel log) + consumer
// dead-lettering. Profil serverless/Cloud Run push → Functions 05d / 06d.
var notificationConnection = builder.Configuration.GetConnectionString("notificationdb")
    ?? throw new InvalidOperationException("ConnectionStrings:notificationdb tidak diset (Aspire WithReference).");

builder.Services.AddNotification(notificationConnection);

// messaging transport (ADR-0029 amendment): RabbitMQ broker bila ConnectionStrings:rabbitmq tersedia (Aspire)
// → consume 2 event lintas-proses NYATA → enqueue delivery; else in-proc fallback (integration test 1-proses).
var rabbitConn = builder.Configuration.GetConnectionString("rabbitmq");
if (!string.IsNullOrWhiteSpace(rabbitConn))
    builder.Services.AddRabbitMqMessaging(rabbitConn, "notification");
else
    builder.Services.AddLocalMessaging();

builder.Services.AddConsumerDeadLettering();
builder.Services.AddLocalNotificationChannels();

// Phase 04d: gRPC read-API client Auth (recipient detail) + MasterData (warehouse context) — ADR-0011 +
// RESILIENCE split-timeout (ADR-0020 pipeline "wms-grpc") + s2s token (ADR-0021 Local stub kosong). Address
// via Aspire service discovery. ConfigureChannel: h2c insecure + CallCredentials (bearer+correlation) →
// UnsafeUseInsecureChannelCallCredentials agar metadata terkirim di Local. Adapter directory di-wire host.
builder.Services.AddGrpcResiliencePipeline();
builder.Services.AddLocalServiceTokenProvider();
builder.Services.AddWmsInternalGrpcClient<AuthReadApi.AuthReadApiClient>("https://auth", "auth");
builder.Services.AddWmsInternalGrpcClient<MasterDataReadApi.MasterDataReadApiClient>("https://masterdata", "masterdata");
builder.Services.AddNotificationDirectories();

// REST terima enum (SubscriberType/NotificationChannel) sebagai NAMA, bukan angka
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// correlation-id sedini mungkin → tiap log/trace request berbagi korelator (ADR-0024 baseline)
app.UseCorrelationId();

// What: consumer subscribe-point (ADR-0029 amendment) — sambungkan dispatcher Notification ke rail, PER event.
// Why: kini AKTIF lintas-proses — IMessageSubscriber = adapter RabbitMQ (queue "notification" bind "#") saat
// broker ada, atau in-proc saat fallback test. Tiap notifier di-subscribe terpisah dgn DLQ source =
// HandlerType-nya → poison ter-atribusi tepat.
// How: tiap dispatcher.HandleXxxAsync dibungkus ConsumerDeadLetterPipeline (retry → DLQ, Phase 02b).
var subscriber = app.Services.GetRequiredService<IMessageSubscriber>();
var dispatcher = app.Services.GetRequiredService<NotificationIntegrationEventDispatcher>();
var deadLettering = app.Services.GetRequiredService<ConsumerDeadLetterPipeline>();
subscriber.Subscribe(deadLettering.Wrap(
    GoodsReceiptConfirmedNotifier.HandlerType, dispatcher.HandleGoodsReceiptConfirmedAsync));
subscriber.Subscribe(deadLettering.Wrap(
    PickingCompletedNotifier.HandlerType, dispatcher.HandlePickingCompletedAsync));

app.MapDefaultEndpoints();
app.MapGet("/", () => "Wms.Notification.Host.Local — Phase 04d (async delivery + idempotency + retry/DLQ)");
app.MapNotificationEndpoints();

app.Run();

// penanda untuk WebApplicationFactory<Program> (integration test in-proc)
public partial class Program;
