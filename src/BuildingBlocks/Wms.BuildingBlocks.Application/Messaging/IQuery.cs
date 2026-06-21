using MediatR;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.BuildingBlocks.Application.Messaging;

// What: CQRS — Query marker, read-side (ADR-0004); MediatR request → Result<T>
// Why: query baca langsung ke read-DTO (bypass aggregate/repository) — sengaja TAK
// ber-ICommandBase, jadi TransactionBehavior melewatinya: tak ada transaksi untuk read.
public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
