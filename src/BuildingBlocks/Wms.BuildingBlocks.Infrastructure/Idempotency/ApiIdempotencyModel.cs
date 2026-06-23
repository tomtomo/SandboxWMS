using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Idempotency;
using Wms.BuildingBlocks.Infrastructure.Messaging;

namespace Wms.BuildingBlocks.Infrastructure.Idempotency;

// What: mapping tabel api_idempotency (ADR-0032) — ko-lokasi schema "infrastructure"
// Why: DIPISAH dari AddInfrastructureTables supaya rollout INKREMENTAL — reference host (MasterData) dulu
// memanggilnya di OnModelCreating → SATU migration; rollout penuh memanggilnya dari tiap write DbContext
// (atau fold ke AddInfrastructureTables) + migration per modul. Kepemilikan infra-table sama (ADR-0010
// amendment): dimiliki & dimigrasi DbContext yang memanggil.
// How: extension ModelBuilder; composite natural key (endpoint, idempotency_key) = in-flight guard;
// snake_case dari UseSnakeCaseNamingConvention (provider, sisi UseNpgsql).
public static class ApiIdempotencyModel
{
    public static ModelBuilder AddApiIdempotencyTable(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiIdempotencyRecord>(b =>
        {
            b.ToTable("api_idempotency", InfrastructureModel.Schema);
            b.HasKey(x => new { x.Endpoint, x.IdempotencyKey });
            b.Property(x => x.Endpoint).HasMaxLength(200);
            b.Property(x => x.IdempotencyKey).HasMaxLength(255);
            b.Property(x => x.ResponseBody).HasMaxLength(8192);
            b.Property(x => x.Traceparent).HasMaxLength(64);
            // index pendukung TTL cleanup (DELETE WHERE recorded_at < now()-24h) — job/trigger (ops/adapter)
            b.HasIndex(x => x.RecordedAt);
        });
        return modelBuilder;
    }
}
