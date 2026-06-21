using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Behaviors;

namespace Wms.BuildingBlocks.Application.DependencyInjection;

// What: composition urutan pipeline behavior (CQRS kernel; ADR-0004 amendment)
// Why: URUTAN behavior adalah kontrak arsitektur — Logging → Authorization → Validation →
// AuditLog → Transaction → Handler — sehingga authz & validation fail-fast SEBELUM audit/
// transaksi, dan AuditLog MEMBUNGKUS Transaction (ADR-0022): audit menulis SETELAH Transaction
// (di dalam) commit/rollback → attempt yang gagal-bisnis tetap terekam out-of-band. Dikunci di
// SATU tempat (BuildingBlocks) agar tiap modul mewarisi urutan identik; MediatR mengeksekusi
// behavior sesuai urutan registrasi (terdaftar lebih dulu = lebih luar).
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
        // AuditLog OUTER terhadap Transaction (membungkusnya) → audit attempt gagal-bisnis yang
        // di-rollback tetap tertulis out-of-band (ADR-0022). Keputusan sadar: ditaruh INNER ke
        // Validation (command yang gagal-validasi short-circuit sebelum eksekusi bisnis → bukan
        // "attempt" yang menyentuh aggregate; bila kelak butuh audit penolakan-validasi/authz,
        // geser AuditLog ke luar Validation/Authorization — itu trigger tinjau-ulang, bukan default).
        configuration.AddOpenBehavior(typeof(AuditLogBehavior<,>));
        configuration.AddOpenBehavior(typeof(TransactionBehavior<,>));
        return configuration;
    }
}
