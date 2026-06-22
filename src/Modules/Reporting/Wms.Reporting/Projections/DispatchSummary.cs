namespace Wms.Reporting.Projections;

// What: Read-model / Projection (CQRS read-side; ADR-0017) — agregat dispatch per hari
// Why: berapa wave dispatched per hari + total volume (overview §F). Di-build dari StockRemoved (Inventory
// emit saat Picked→removed, ADR-0030): WaveCount++, TotalVolume += Σ qty lines. Di-derive dari event
// kepemilikan-Inventory (bukan outbound.shipment_dispatched yang tak bawa qty). Day dari OccurredAt event
// (rebuild deterministik).
// How: PK Day; mutasi via AddDispatch; store find-or-create-by-PK (ADR-0017).
public sealed class DispatchSummary
{
    public DateOnly Day { get; private set; }

    public int WaveCount { get; private set; }

    public int TotalVolume { get; private set; }

    private DispatchSummary() { }

    public DispatchSummary(DateOnly day) => Day = day;

    public void AddDispatch(int volume)
    {
        WaveCount += 1;
        TotalVolume += volume;
    }
}
