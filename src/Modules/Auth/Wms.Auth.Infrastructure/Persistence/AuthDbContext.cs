using Microsoft.EntityFrameworkCore;
using Wms.Auth.Domain;
using Wms.BuildingBlocks.Infrastructure.Messaging;

namespace Wms.Auth.Infrastructure.Persistence;

// What: DbContext modul Auth (DDD Unit of Work; ADR-0010 DB-per-service)
// Why: Auth memiliki datanya sendiri — schema "auth" (User/Role/Permission/RefreshToken) + tabel rail
// "infrastructure" (audit_log dipakai utk admin writes; outbox/inbox/dead_letter idle — Auth read-only ke
// core, tak emit/consume event di core flow, ADR-0011). Read-API gRPC membaca DbContext ini via read-port
// (reader-delegation), BUKAN gRPC `.Api` langsung (FF#8).
// How: HasDefaultSchema("auth"); AddInfrastructureTables memetakan rail; konfigurasi aggregate via
// ApplyConfigurationsFromAssembly; snake_case di seam UseNpgsql.
public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public const string Schema = "auth";

    // What: flag bypass soft-delete TER-TARGET Role (ADR-0014 amendment, mirror MasterData 04a)
    // Why: EF Core 8 belum punya NAMED query filters (EF 10) → pola FLAG-GATED di filter tunggal mencapai
    // targeting setara: relaks HANYA isActive (`IncludeInactive || IsActive`), BUKAN blanket
    // IgnoreQueryFilters. internal: hanya reader/repo (assembly ini) yang men-toggle, reset try/finally.
    internal bool IncludeInactive { get; set; }

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.AddInfrastructureTables();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);

        // What: global soft-delete query filter Role (ADR-0014) — flag-gated targeted bypass.
        // Why: HANYA Role yang dijaga filter — permission dari role non-aktif tak boleh bocor ke jalur
        // mint/claim (ADR-0012). User TIDAK difilter: jalur Login butuh memuat user Disabled/Locked untuk
        // mengembalikan error seragam & mencatat failed-login (status dicek eksplisit di handler).
        modelBuilder.Entity<Role>().HasQueryFilter(role => IncludeInactive || role.IsActive);

        base.OnModelCreating(modelBuilder);
    }
}
