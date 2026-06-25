using System.Net.Security;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Security;

namespace Wms.BuildingBlocks.Web.Grpc;

// What: composition helper internal s2s gRPC client (ADR-0006/0021) — satu titik wiring channel+credentials
// Why: wiring AddGrpcClient + channel TLS + bearer audience-scoped sebelumnya di-copy-paste tiap host (5 site,
// drift-prone). Disatukan agar SATU titik swap transport — UF-26 pre-position, kini DI-EKSEKUSI (h2c → TLS).
// **Kenapa TLS, bukan h2c lagi:** gRPC butuh HTTP/2; di endpoint CLEARTEXT, Kestrel `Http1AndHttp2` TAK bisa
// negosiasi H2 (tanpa TLS/ALPN) → semua koneksi default H1 → gRPC gagal `HTTP_1_1_REQUIRED` (MS Learn:
// Kestrel protocol negotiation). MasterData/Auth serve REST(H1)+gRPC(H2) di satu host → endpoint H2 harus
// TLS (ALPN) — itulah endpoint `https://` service (sudah ada di launchSettings, dev cert otomatis). Channel
// kini TLS-encrypted → STRICTLY lebih aman dari h2c insecure sebelumnya.
// How: ConfigurePrimaryHttpMessageHandler set SslOptions transport (tak ganggu named resilience pipeline
// terpisah, ADR-0020); ConfigureChannel set ChannelCredentials.SecureSsl + CallCredentials (bearer audience).
// CATATAN: TIDAK melipat AddGrpcResiliencePipeline/Enqueue (concern beda — resilience & outbox terpisah).
public static class GrpcClientExtensions
{
    public static IHttpClientBuilder AddWmsInternalGrpcClient<TClient>(
        this IServiceCollection services, string address, string audience)
        where TClient : class =>
        services.AddGrpcClient<TClient>(options => options.Address = new Uri(address))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                SslOptions = new SslClientAuthenticationOptions
                {
                    // Local: dev cert (CN=localhost) valid tapi belum tentu trusted di OS store → accept.
                    // Channel TETAP TLS-encrypted (lebih aman dari h2c). Phase 05/07 (cloud): managed cert +
                    // validasi nyata = ganti callback ini SAJA (single swap point).
                    RemoteCertificateValidationCallback = (_, _, _, _) => true,
                },
            })
            .ConfigureChannel((serviceProvider, channel) =>
                channel.Credentials = ChannelCredentials.Create(
                    ChannelCredentials.SecureSsl,
                    ServiceAuthCallCredentials.Create(
                        serviceProvider.GetRequiredService<IServiceTokenProvider>(), audience)));
}
