using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Behaviors;

namespace Wms.BuildingBlocks.Application.DependencyInjection;

// What: composition urutan pipeline behavior (CQRS kernel; ADR-0004 amendment)
// Why: URUTAN behavior adalah kontrak arsitektur — Logging → Authorization → Validation →
// Transaction → Handler — sehingga authz & validation fail-fast SEBELUM transaksi dibuka.
// Dikunci di SATU tempat (BuildingBlocks) agar tiap modul mewarisi urutan identik; MediatR
// mengeksekusi behavior sesuai urutan registrasi.
// How: tiap modul memanggil AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(modul);
// cfg.AddBuildingBlocksBehaviors(); }) — open-generic behavior di-close per request oleh DI.
public static class ApplicationConfiguration
{
    public static MediatRServiceConfiguration AddBuildingBlocksBehaviors(
        this MediatRServiceConfiguration configuration)
    {
        configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
        configuration.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
        configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        configuration.AddOpenBehavior(typeof(TransactionBehavior<,>));
        // SLOT 02c: AuditLogBehavior (ADR-0022) — outcome-aware, out-of-band, command-only.
        // Sengaja BELUM di-wire di 02a (placeholder note); ditambah penuh di Phase 02c.
        return configuration;
    }
}
