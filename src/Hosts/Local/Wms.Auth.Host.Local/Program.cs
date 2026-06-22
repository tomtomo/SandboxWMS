using Microsoft.AspNetCore.Server.Kestrel.Core;
using Wms.Auth.Api;
using Wms.Auth.Api.Grpc;
using Wms.Auth.Application.DependencyInjection;
using Wms.Auth.Infrastructure.DependencyInjection;
using Wms.BuildingBlocks.Web.Correlation;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.BuildingBlocks.Web.Security;
using Wms.Platform.Hosting;
using Wms.Platform.Local.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// service defaults agnostic: health + service discovery + HTTP resilience (split-timeout) + OTel (ADR-0008)
builder.AddServiceDefaults();

// Auth = supporting authority (overview §E): expose gRPC read-API (service-to-service, ADR-0006/0011) +
// REST login/refresh/logout. gRPC butuh HTTP/2; REST HTTP/1.1 → Kestrel Http1AndHttp2 (h2c di Local).
// READ-ONLY ke core: TANPA rail messaging/outbox/consumer; audit_log dipakai utk admin writes. Seeding
// admin/permission = tanggung jawab MigrationRunner (DB-prep), BUKAN host (hindari race + boot test).
builder.WebHost.ConfigureKestrel(options =>
    options.ConfigureEndpointDefaults(listen => listen.Protocols = HttpProtocols.Http1AndHttp2));

var authConnection = builder.Configuration.GetConnectionString("authdb")
    ?? throw new InvalidOperationException("ConnectionStrings:authdb tidak diset (Aspire WithReference).");

builder.Services.AddAuthInfrastructure(authConnection);
builder.Services.AddAuthApplication();
builder.Services.AddLocalCaching();            // ICacheStore untuk cache-aside read-API
builder.Services.AddLocalPasswordHasher();     // Argon2id — verify login + hash seed
builder.Services.AddLocalSecretProvider();     // RS256 key (private signing issuer + public validation)
builder.Services.AddHttpContextCurrentUser();  // identitas dari JWT/HttpContext → IAuditable (created_by)
builder.Services.AddLocalAuditing();
builder.Services.AddWmsJwtBearer();            // host ini juga validasi user token offline (alg-pin RS256)

// gRPC server + interceptor Result→RpcException (ADR-0019: status mapping tak tersebar di service)
builder.Services.AddGrpc(options => options.Interceptors.Add<ResultExceptionInterceptor>());

var app = builder.Build();

// correlation-id sedini mungkin → tiap log/trace/audit berbagi korelator (ADR-0024 baseline)
app.UseCorrelationId();

// authentication: validasi bearer (offline) → isi HttpContext.User → ICurrentUser identitas nyata
app.UseAuthentication();

app.MapGrpcService<AuthReadService>();
app.MapAuthEndpoints();
app.MapDefaultEndpoints();
app.MapGet("/", () => "Wms.Auth.Host.Local — Phase 04b (JWT RS256 + refresh rotation + Argon2id)");

app.Run();

// penanda kelas Program untuk WebApplicationFactory<Program> di integration test
public partial class Program;
