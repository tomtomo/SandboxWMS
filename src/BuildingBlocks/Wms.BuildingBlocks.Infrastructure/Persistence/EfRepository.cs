using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.BuildingBlocks.Infrastructure.Persistence;

// What: implementasi EF Core generik untuk IRepository<TAggregate, TId> (DDD; ADR-0010/0011)
// Why: modul dapat Add/GetById tanpa re-implement boilerplate — repository konkret tinggal turunkan
// base ini lalu menambah query bermakna-domain terhadap DbSet terlindungi. Commit dipisah ke IUnitOfWork.
// How: DbSet di-resolve via Context.Set<TAggregate>() (tak perlu properti DbSet di context). GetById
// pakai FirstOrDefault dengan predicate Id — BUKAN Find — supaya global query filter modul
// (mis. soft-delete aktif-only) tetap berlaku. Add sync (AddAsync hanya perlu untuk value generator
// khusus spt HiLo; strongly-typed id surrogate tak butuh).
public abstract class EfRepository<TAggregate, TId, TContext>(TContext context)
    : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
    where TContext : DbContext
{
    protected TContext Context { get; } = context;

    protected DbSet<TAggregate> DbSet => Context.Set<TAggregate>();

    public virtual Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(aggregate => aggregate.Id.Equals(id), cancellationToken);

    public virtual Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        DbSet.Add(aggregate);
        return Task.CompletedTask;
    }
}
