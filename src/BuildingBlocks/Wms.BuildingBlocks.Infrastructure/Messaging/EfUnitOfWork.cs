using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Abstractions;

namespace Wms.BuildingBlocks.Infrastructure.Messaging;

// What: adapter IUnitOfWork berbasis EF Core
// Why: commit lewat DbContext ambient (DB-per-service: satu DbContext per host),
// sehingga state aggregate + baris outbox masuk dalam satu transaksi. Application
// tak tahu EF — ini sisi Infrastructure dari port.
internal sealed class EfUnitOfWork(DbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
