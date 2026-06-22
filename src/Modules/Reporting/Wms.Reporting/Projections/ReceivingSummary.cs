namespace Wms.Reporting.Projections;

// What: Read-model / Projection (CQRS read-side; ADR-0017) — agregat penerimaan per (supplier, hari)
// Why: supplier performance + discrepancy rate per periode (overview §F). Di-build dari GRConfirmed:
// GrCount++, ReceivedQty += Σ receivedLines, RejectedQty += Σ rejectedLines. Discrepancy rate dihitung
// saat query (RejectedQty / total). SupplierId NON-NULL ("" = tak diketahui) untuk PK; Day dari OccurredAt
// event (BUKAN wall-clock) → rebuild deterministik.
// How: PK komposit (SupplierId, Day); mutasi via AddReceipt; store find-or-create-by-PK (ADR-0017).
public sealed class ReceivingSummary
{
    // "" = supplier tak diketahui (GRConfirmedV1.supplierId nullable) — PK tak boleh null
    public string SupplierId { get; private set; } = null!;

    public DateOnly Day { get; private set; }

    public int GrCount { get; private set; }

    public int ReceivedQty { get; private set; }

    public int RejectedQty { get; private set; }

    private ReceivingSummary() { }

    public ReceivingSummary(string supplierId, DateOnly day)
    {
        SupplierId = supplierId;
        Day = day;
    }

    public void AddReceipt(int receivedQty, int rejectedQty)
    {
        GrCount += 1;
        ReceivedQty += receivedQty;
        RejectedQty += rejectedQty;
    }
}
