using System.Text.Json.Serialization;
using Wms.BuildingBlocks.Infrastructure.DependencyInjection;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.BuildingBlocks.Infrastructure.Resilience;
using Wms.BuildingBlocks.Web.Correlation;
using Wms.BuildingBlocks.Web.Grpc;
using Wms.BuildingBlocks.Web.Security;
using Wms.MasterData.Grpc;
using Wms.Outbound.Api;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Application.DependencyInjection;
using Wms.Outbound.Application.Features.ConsumeStockAllocated;
using Wms.Outbound.Application.Features.ConsumeStockAllocationFailed;
using Wms.Outbound.Infrastructure.DependencyInjection;
using Wms.Outbound.Infrastructure.MasterData;
using Wms.Outbound.Infrastructure.Messaging;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.Platform.Hosting;
using Wms.Platform.Local.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// service defaults agnostic: health + service discovery + HTTP resilience + OTel (ADR-0008)
builder.AddServiceDefaults();

// Outbound = modul HYBRID (Phase 03c): producer 3 event (WaveReleased/PickingCompleted/ShipmentDispatched
// via Outbox) + consumer StockAllocated (→ PickingTask) + REST SPV/operator. DbContext (outbound + inbox/
// outbox/dead_letter/audit_log) + adapter Local + Outbox dispatcher (AKTIF) + consumer dead-lettering.
var outboundConnection = builder.Configuration.GetConnectionString("outbounddb")
    ?? throw new InvalidOperationException("ConnectionStrings:outbounddb tidak diset (Aspire WithReference).");

builder.Services.AddOutboundInfrastructure(outboundConnection);
builder.Services.AddOutboundApplication();

// messaging transport (ADR-0029 amendment): RabbitMQ broker bila ConnectionStrings:rabbitmq tersedia (Aspire)
// → cross-process delivery NYATA (emit 3 event via Outbox + consume StockAllocated); else in-proc fallback.
var rabbitConn = builder.Configuration.GetConnectionString("rabbitmq");
if (!string.IsNullOrWhiteSpace(rabbitConn))
    builder.Services.AddRabbitMqMessaging(rabbitConn, "outbound");
else
    builder.Services.AddLocalMessaging();

builder.Services.AddOutboxDispatcher();
builder.Services.AddConsumerDeadLettering();

// host melayani REST → identitas dari HttpContext/JWT (ICurrentUser) untuk CreateWave/CompletePicking/
// DispatchWave + audit-log store Local (AuditLogBehavior). KONSUMER tetap origin-MESIN: scope event (tanpa
// HttpContext) → HttpContextCurrentUser resolve SYSTEM (ADR-0027) → created_by=SYSTEM saat consumer menulis
// PickingTask; request REST membawa identitas operator. Satu host, dua origin — aman.
builder.Services.AddHttpContextCurrentUser();
builder.Services.AddLocalAuditing();

// JSON string-enum agar read endpoint menserialisasi Status (OutboundOrderStatus/WaveStatus/
// PickingTaskStatus) sebagai NAMA (bukan angka) + parity binding filter ?status= (copy dari Inbound host).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Phase 04b: validasi user JWT OFFLINE (ADR-0016 alg-pin RS256) — public key via ISecretProvider (env
// AppHost / ephemeral Local). Token valid → HttpContext.User terisi → ICurrentUser identitas NYATA (ganti
// anonymous) untuk request REST. Konsumer event tetap origin-MESIN → SYSTEM (ADR-0027). authZ deferred.
builder.Services.AddLocalSecretProvider();
builder.Services.AddWmsJwtBearer();

// Phase 04a: gRPC read-API client MasterData (snapshot uom orderLines, ADR-0011/0014) + RESILIENCE
// split-timeout (ADR-0020: pipeline "wms-grpc" 30s) + s2s token (ADR-0021: Local stub). Address via
// Aspire service discovery ("masterdata"); h2c insecure + CallCredentials (bearer+correlation).
builder.Services.AddGrpcResiliencePipeline();
builder.Services.AddLocalServiceTokenProvider();
builder.Services.AddWmsInternalGrpcClient<MasterDataReadApi.MasterDataReadApiClient>("https://masterdata", "masterdata");
builder.Services.AddMasterDataProductCatalog();

var app = builder.Build();

// correlation-id sedini mungkin → tiap log/trace/audit request berbagi korelator (ADR-0024 baseline)
app.UseCorrelationId();

// authentication: validasi bearer offline → isi principal sebelum endpoint/handler (ICurrentUser nyata, 04b)
app.UseAuthentication();

// What: consumer subscribe-point (ADR-0029 amendment) — sambungkan dispatcher Outbound ke rail, PER event.
// Why: kini AKTIF lintas-proses — IMessageSubscriber = adapter RabbitMQ (queue "outbound" bind "#") saat broker
// ada, atau in-proc saat fallback test. Consumer di-subscribe dengan DLQ source = HandlerType-nya → poison
// ter-atribusi ke consumer yang benar (forensik granular).
// How: dispatcher.HandleStockAllocatedAsync dibungkus ConsumerDeadLetterPipeline (retry → DLQ, Phase 02b).
var subscriber = app.Services.GetRequiredService<IMessageSubscriber>();
var dispatcher = app.Services.GetRequiredService<OutboundIntegrationEventDispatcher>();
var deadLettering = app.Services.GetRequiredService<ConsumerDeadLetterPipeline>();
subscriber.Subscribe(deadLettering.Wrap(
    StockAllocatedConsumer.HandlerType, dispatcher.HandleStockAllocatedAsync));
subscriber.Subscribe(deadLettering.Wrap(
    StockAllocationFailedConsumer.HandlerType, dispatcher.HandleStockAllocationFailedAsync));

app.MapDefaultEndpoints();
app.MapGet("/", () => "Wms.Outbound.Host.Local — Phase 03c (OutboundOrder/Wave/PickingTask + 3 event emit + StockAllocated consumer)");
app.MapOutboundEndpoints();

app.Run();
