namespace Wms.BuildingBlocks.Application.Abstractions;

// What: exception sinyal optimistic-concurrency conflict (ADR-0031) — abstraksi Application-level
// Why: EfUnitOfWork (Infrastructure) menerjemahkan DbUpdateConcurrencyException (EF Core) → exception ini
// supaya TransactionBehavior (Application, NOL-EF) bisa menangkapnya tanpa kenal EF, lalu map ke
// Result(Error.Conflict). Menjaga "Application tak tahu EF" (Hexagonal, ADR-0002).
// How: dilempar di seam tulis (EfUnitOfWork.SaveChangesAsync); ditangkap di TransactionBehavior.
public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("Optimistic concurrency conflict — sumber daya diubah transaksi lain.", innerException);
