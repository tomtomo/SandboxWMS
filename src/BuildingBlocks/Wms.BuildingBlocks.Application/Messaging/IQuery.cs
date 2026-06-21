namespace Wms.BuildingBlocks.Application.Messaging;

// What: CQRS — Query marker, read-side (ADR-0004)
// Why: query bypass aggregate/repository, baca langsung ke read-DTO — tak buka transaksi.
public interface IQuery<out TResponse>
{
}
