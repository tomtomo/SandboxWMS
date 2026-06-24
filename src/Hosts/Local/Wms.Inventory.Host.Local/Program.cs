using System.Text.Json.Serialization;
using Wms.BuildingBlocks.Infrastructure.DependencyInjection;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.BuildingBlocks.Infrastructure.Resilience;
using Wms.BuildingBlocks.Web.Correlation;
using Wms.BuildingBlocks.Web.Grpc;
using Wms.BuildingBlocks.Web.Security;
using Wms.MasterData.Grpc;
using Wms.Inventory.Api;
using Wms.Inventory.Application.DependencyInjection;
using Wms.Inventory.Application.Features.ConsumeGoodsReceiptConfirmed;
using Wms.Inventory.Application.Features.ConsumePickingCompleted;
using Wms.Inventory.Application.Features.ConsumeShipmentDispatched;
using Wms.Inventory.Application.Features.ConsumeWaveReleased;
using Wms.Inventory.Infrastructure.DependencyInjection;
using Wms.Inventory.Infrastructure.Messaging;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.Platform.Hosting;
using Wms.Platform.Local.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// service defaults agnostic: health + service discovery + HTTP resilience + OTel (ADR-0008)
builder.AddServiceDefaults();

// Inventory = modul HYBRID (Phase 03b): consumer 4 event lintas-context + REST operator (CompletePutaway).
// DbContext (inventory + inbox/outbox/dead_letter/audit_log) + adapter Local + Outbox dispatcher (kini
// AKTIF — Inventory emit StockAllocated) + consumer dead-lettering.
var inventoryConnection = builder.Configuration.GetConnectionString("inventorydb")
    ?? throw new InvalidOperationException("ConnectionStrings:inventorydb tidak diset (Aspire WithReference).");

builder.Services.AddInventoryInfrastructure(inventoryConnection);
builder.Services.AddInventoryApplication();

// JSON string-enum agar StockStatus/PutawayTaskStatus diserialisasi sebagai NAMA (bukan angka) di
// response read-API DAN query-filter ?status= ter-bind dari nama enum (ListStocks/ListPutawayTasks).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// messaging transport (ADR-0029 amendment): RabbitMQ broker bila ConnectionStrings:rabbitmq tersedia (Aspire)
// → cross-process delivery NYATA; else in-proc fallback (integration test 1-proses / run tanpa broker).
var rabbitConn = builder.Configuration.GetConnectionString("rabbitmq");
if (!string.IsNullOrWhiteSpace(rabbitConn))
    builder.Services.AddRabbitMqMessaging(rabbitConn, "inventory");
else
    builder.Services.AddLocalMessaging();

builder.Services.AddOutboxDispatcher();
builder.Services.AddConsumerDeadLettering();

// Phase 03b: host kini juga melayani REST → identitas dari HttpContext/JWT (ICurrentUser) untuk
// CompletePutaway + audit-log store Local (AuditLogBehavior). KONSUMER tetap origin-MESIN: scope event
// (tanpa HttpContext) → HttpContextCurrentUser resolve SYSTEM (ADR-0027) → created_by=SYSTEM saat consumer
// menulis Stock/PutawayTask; request REST membawa identitas operator. Satu host, dua origin — aman.
builder.Services.AddHttpContextCurrentUser();
builder.Services.AddLocalAuditing();

// Phase 04b: validasi user JWT OFFLINE (ADR-0016 alg-pin RS256) — public key via ISecretProvider (env
// AppHost / ephemeral Local). Token valid → HttpContext.User terisi → ICurrentUser identitas NYATA (ganti
// anonymous) untuk request REST. Konsumer event tetap origin-MESIN → SYSTEM (ADR-0027). authZ deferred.
builder.Services.AddLocalSecretProvider();
builder.Services.AddWmsJwtBearer();

// Phase 04a follow-up: gRPC read-API client MasterData (resolve default location receiving/quarantine,
// ADR-0011) + RESILIENCE split-timeout (ADR-0020 pipeline "wms-grpc" 30s) + s2s token (ADR-0021 Local
// stub). Address via Aspire service discovery ("masterdata"); h2c insecure + CallCredentials.
builder.Services.AddGrpcResiliencePipeline();
builder.Services.AddLocalServiceTokenProvider();
builder.Services.AddWmsInternalGrpcClient<MasterDataReadApi.MasterDataReadApiClient>("http://masterdata", "masterdata");
builder.Services.AddMasterDataLocationCatalog();

var app = builder.Build();

// correlation-id sedini mungkin → tiap log/trace/audit request berbagi korelator (ADR-0024 baseline)
app.UseCorrelationId();

// authentication: validasi bearer offline → isi principal sebelum endpoint/handler (ICurrentUser nyata, 04b)
app.UseAuthentication();

// What: consumer subscribe-point (ADR-0029 amendment) — sambungkan dispatcher Inventory ke rail, PER event.
// Why: kini AKTIF lintas-proses — IMessageSubscriber = adapter RabbitMQ (queue "inventory" bind "#" di
// exchange "wms.events") saat broker ada, atau in-proc saat fallback test. Tiap consumer di-subscribe terpisah
// dengan DLQ source = HandlerType-nya → poison ter-atribusi ke consumer yang benar (forensik granular).
// How: tiap dispatcher.HandleXxxAsync dibungkus ConsumerDeadLetterPipeline (retry → DLQ, Phase 02b).
var subscriber = app.Services.GetRequiredService<IMessageSubscriber>();
var dispatcher = app.Services.GetRequiredService<InventoryIntegrationEventDispatcher>();
var deadLettering = app.Services.GetRequiredService<ConsumerDeadLetterPipeline>();
subscriber.Subscribe(deadLettering.Wrap(
    GoodsReceiptConfirmedConsumer.HandlerType, dispatcher.HandleGoodsReceiptConfirmedAsync));
subscriber.Subscribe(deadLettering.Wrap(
    WaveReleasedConsumer.HandlerType, dispatcher.HandleWaveReleasedAsync));
subscriber.Subscribe(deadLettering.Wrap(
    PickingCompletedConsumer.HandlerType, dispatcher.HandlePickingCompletedAsync));
subscriber.Subscribe(deadLettering.Wrap(
    ShipmentDispatchedConsumer.HandlerType, dispatcher.HandleShipmentDispatchedAsync));

app.MapDefaultEndpoints();
app.MapGet("/", () => "Wms.Inventory.Host.Local — Phase 03b (Stock lifecycle + FEFO + 4 event consumers + CompletePutaway)");
app.MapInventoryEndpoints();

app.Run();
