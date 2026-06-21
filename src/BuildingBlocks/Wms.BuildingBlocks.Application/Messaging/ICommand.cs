using MediatR;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.BuildingBlocks.Application.Messaging;

// What: CQRS — Command marker base (ADR-0004)
// Why: satu diskriminator non-generic yang dipakai TransactionBehavior untuk mengenali sisi
// WRITE — transaksi hanya dibuka untuk command; query bypass (ADR-0004 amendment). Dipisah
// dari varian generic supaya command void & ber-nilai berbagi penanda yang sama.
public interface ICommandBase
{
}

// What: CQRS — Command tanpa nilai balik (ADR-0004); MediatR request → Result
// Why: write-intent eksplisit yang menghasilkan Result (sukses/gagal sebagai NILAI, ADR-0019),
// bukan exception — pipeline mengembalikan Result, dipetakan ke transport di tepi.
public interface ICommand : ICommandBase, IRequest<Result>
{
}

// What: CQRS — Command dengan nilai balik (ADR-0004); MediatR request → Result<T>
public interface ICommand<TResponse> : ICommandBase, IRequest<Result<TResponse>>
{
}
