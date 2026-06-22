namespace Wms.Reporting.Projections;

// What: Read-model / Projection (CQRS read-side; ADR-0017) — produktivitas operator per (operator, hari)
// Why: throughput per operator untuk KPI (overview §F). Di-build dari PutawayCompleted (putaway-count) +
// PickingCompleted (pick-count). OperatorId NON-NULL ("" = SYSTEM/tak diketahui) untuk PK — atribusi
// per-operator NYATA menyala saat authZ wire-up (07a, ADR-0012); s/d itu operator = SYSTEM. Day dari
// OccurredAt event (rebuild deterministik).
// How: PK komposit (OperatorId, Day); mutasi via RecordPutaway/RecordPick; store find-or-create-by-PK.
// Catatan: scan-count (overview §F) tak di-scope — tak ada integration event untuk ScanItem (Inbound
// internal); §F mapping hanya memetakan Putaway/Picking-Completed → OperatorActivity.
public sealed class OperatorActivity
{
    // "" = SYSTEM / tak diketahui (operatorId nullable; authZ deferred) — PK tak boleh null
    public string OperatorId { get; private set; } = null!;

    public DateOnly Day { get; private set; }

    public int PutawayCount { get; private set; }

    public int PickCount { get; private set; }

    private OperatorActivity() { }

    public OperatorActivity(string operatorId, DateOnly day)
    {
        OperatorId = operatorId;
        Day = day;
    }

    public void RecordPutaway() => PutawayCount += 1;

    public void RecordPick() => PickCount += 1;
}
