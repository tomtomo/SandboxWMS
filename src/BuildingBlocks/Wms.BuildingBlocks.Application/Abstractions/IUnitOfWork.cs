namespace Wms.BuildingBlocks.Application.Abstractions;

// What: Port — Unit of Work (Fowler PoEAA; ADR-0005)
// Why: commit perubahan aggregate + baris Outbox dalam SATU transaksi (atomicity
// state+event, anti dual-write). Application memicu commit tanpa tahu EF/DbContext —
// adapter konkret (Infrastructure) yang memetakannya ke SaveChanges.
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
