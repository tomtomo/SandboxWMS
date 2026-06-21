using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wms.BuildingBlocks.Application.Security;
using Wms.BuildingBlocks.Domain.Auditing;

namespace Wms.BuildingBlocks.Infrastructure.Auditing;

// What: EF SaveChanges Interceptor — auto-populate IAuditable (ADR-0027; overview §Konvensi)
// Why: createdBy/createdAt/modifiedBy/modifiedAt tak boleh diisi manual di tiap handler
// (tersebar, mudah lupa, rawan inkonsisten) — satu interceptor menstempelnya seragam tepat
// sebelum INSERT/UPDATE, dari principal aktif (ICurrentUser → SYSTEM untuk consumer/job, userId
// untuk HTTP). Cross-cutting di Infrastructure: Domain tetap tak tahu siapa pelakunya.
// How: didaftarkan SCOPED + ditambahkan via AddDbContext((sp,options) => AddInterceptors(...))
// agar ICurrentUser scoped ter-inject benar per request/message. Pada SavingChanges, scan
// ChangeTracker.Entries<IAuditable>() lalu set nilai lewat entry.Property(...).CurrentValue —
// menembus private setter aggregate (enkapsulasi domain terjaga; tak ada public setter).
public sealed class AuditableEntityInterceptor(ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
            return;

        var actor = currentUser.UserId;
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue = actor;
                entry.Property(nameof(IAuditable.CreatedAt)).CurrentValue = now;
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property(nameof(IAuditable.ModifiedBy)).CurrentValue = actor;
                entry.Property(nameof(IAuditable.ModifiedAt)).CurrentValue = now;
            }
        }
    }
}
