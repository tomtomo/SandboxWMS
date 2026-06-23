using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Wms.BuildingBlocks.Application.Abstractions;

namespace Wms.BuildingBlocks.Infrastructure.Messaging;

// What: adapter IUnitOfWork berbasis EF Core
// Why: commit lewat DbContext ambient (DB-per-service: satu DbContext per host),
// sehingga state aggregate + baris outbox masuk dalam satu transaksi. Application
// tak tahu EF — ini sisi Infrastructure dari port.
internal sealed class EfUnitOfWork(DbContext db) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // optimistic concurrency token (xmin) stale → terjemahkan ke abstraksi Application (ADR-0031)
            // supaya TransactionBehavior (nol-EF) map ke Result(Error.Conflict) — Application tak tahu EF.
            throw new ConcurrencyConflictException(exception);
        }
    }

    // What: adapter transaksi eksplisit (Hexagonal port→adapter; ADR-0019)
    // Why: TransactionBehavior mengontrol commit/rollback berdasarkan Result — EF
    // IDbContextTransaction dibungkus ITransaction supaya Application tetap nol-EF.
    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => new EfTransaction(await db.Database.BeginTransactionAsync(cancellationToken));

    // What: pembungkus IDbContextTransaction → port ITransaction
    // How: dispose tanpa commit = transaksi di-rollback oleh EF (jaring pengaman bila
    // pipeline keluar tanpa Commit/Rollback eksplisit).
    private sealed class EfTransaction(IDbContextTransaction transaction) : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default)
            => transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default)
            => transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
