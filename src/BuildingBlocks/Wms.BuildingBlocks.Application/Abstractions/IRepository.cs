using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.BuildingBlocks.Application.Abstractions;

// What: Repository port generik (DDD) — operasi write-side universal per aggregate root
// Why: hapus duplikasi mekanis Add/GetById yang sebelumnya ditulis ulang tiap modul. Constraint
// `where TAggregate : AggregateRoot<TId>` menegakkan aturan "repository hanya untuk aggregate root"
// (Evans/Vernon) di level compiler. Sengaja SEMPIT — tanpa GetAll/IQueryable (cegah leaky abstraction);
// query bermakna-domain ditambah di port turunan tiap modul. Read-side dilayani Reader terpisah (CQRS).
// Commit bukan urusan repo — itu IUnitOfWork (TransactionBehavior).
// How: `IGoodsReceiptRepository : IRepository<GoodsReceipt, GoodsReceiptId>`; impl EF dari EfRepository.
public interface IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
}
