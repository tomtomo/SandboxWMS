using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Security;

namespace Wms.BuildingBlocks.Web.Grpc;

// What: composition helper internal s2s gRPC client (ADR-0006/0021) — satu titik wiring channel+credentials
// Why: wiring AddGrpcClient + ConfigureChannel (insecure h2c Local + bearer audience-scoped + correlation)
// sebelumnya di-copy-paste tiap host (5 site, drift-prone). Disatukan agar SATU titik meng-conditionalize
// insecure-flag saat Phase 05 TLS swap (h2c → TLS) — pre-position, bukan menyebar perubahan ke tiap host.
// How: extension IServiceCollection; ServiceAuthCallCredentials (sejawat) mint CallCredentials per audience.
// CATATAN: TIDAK melipat AddGrpcResiliencePipeline/Enqueue (concern beda — resilience & outbox terpisah).
public static class GrpcClientExtensions
{
    public static IHttpClientBuilder AddWmsInternalGrpcClient<TClient>(
        this IServiceCollection services, string address, string audience)
        where TClient : class =>
        services.AddGrpcClient<TClient>(options => options.Address = new Uri(address))
            .ConfigureChannel((serviceProvider, channel) =>
            {
                // Local: channel insecure (h2c) → UnsafeUseInsecureChannelCallCredentials agar bearer terkirim.
                // Phase 05 TLS swap = ganti dua baris ini DI SINI saja (UF-26 pre-position).
                channel.Credentials = ChannelCredentials.Create(
                    ChannelCredentials.Insecure,
                    ServiceAuthCallCredentials.Create(
                        serviceProvider.GetRequiredService<IServiceTokenProvider>(), audience));
                channel.UnsafeUseInsecureChannelCallCredentials = true;
            });
}
