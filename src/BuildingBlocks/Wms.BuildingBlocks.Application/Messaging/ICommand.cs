namespace Wms.BuildingBlocks.Application.Messaging;

// What: CQRS — Command marker (ADR-0004)
// Why: memisahkan write-intent dari read; marker inilah yang dipakai
// TransactionBehavior (Phase 02a) untuk membuka transaksi HANYA di sisi command.
public interface ICommand
{
}

// What: CQRS — Command dengan hasil (ADR-0004)
public interface ICommand<out TResponse>
{
}
