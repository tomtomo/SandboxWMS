namespace Wms.BuildingBlocks.Application.Abstractions;

// What: Port — Unit of Work (Fowler PoEAA; ADR-0005)
// Why: commit perubahan aggregate + baris Outbox dalam SATU transaksi (atomicity
// state+event, anti dual-write). Application memicu commit tanpa tahu EF/DbContext —
// adapter konkret (Infrastructure) yang memetakannya ke SaveChanges.
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // What: buka transaksi eksplisit yang dikontrol TransactionBehavior (ADR-0019)
    // Why: rollback-on-Result.Failure menuntut transaksi yang di-commit HANYA saat sukses —
    // bukan mengandalkan auto-commit per-SaveChanges. Port abstrak menjaga Application nol-EF;
    // adapter membungkus IDbContextTransaction.
    Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

// What: Port — handle transaksi UoW (Fowler PoEAA; ADR-0019)
// Why: TransactionBehavior memutuskan commit (Result.Success) atau rollback (Result.Failure /
// exception) tanpa menyentuh EF; IAsyncDisposable menjamin transaksi yang tak ter-commit
// dibuang aman (efektif rollback) saat scope berakhir.
public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
