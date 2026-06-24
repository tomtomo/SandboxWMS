namespace Wms.Inventory.Application.ReadModels;

// What: read DTO (CQRS read-side; ADR-0004) — ringkasan PutawayTask untuk list UI (WebUI maturity),
// decoupled dari aggregate: Status di-flatten ke string, StockId di-flatten ke Guid (.Value).
public sealed record PutawayTaskListItem(
    Guid PutawayTaskId,
    Guid StockId,
    string SourceLocationId,
    string SuggestedDestinationId,
    string? ActualDestinationId,
    string? AssignedTo,
    string Status);
