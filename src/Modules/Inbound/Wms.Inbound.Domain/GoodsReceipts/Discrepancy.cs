namespace Wms.Inbound.Domain;

// What: satu entry discrepancy (two-axis, ADR-0013) — entity DI DALAM aggregate GoodsReceipt
// Why: dikompilasi saat DeclareScanComplete dari salah satu sumbu (qty atau kondisi). Resolusi
// dimodelkan sebagai ATRIBUT entry ini (Action/Note), bukan koleksi terpisah — menegakkan pairing
// 1:1 "tiap discrepancy punya resolution" sebagai invariant aggregate sebelum Confirm.
// How: Resolve() mengisi Action (private setter); IsResolved = Action.HasValue. Konstruksi &
// resolusi hanya via GoodsReceipt (satu pintu konsistensi).
public sealed class Discrepancy
{
    public string Sku { get; private set; } = null!;

    public DiscrepancyType Type { get; private set; }

    public ResolutionAction? Action { get; private set; }

    public string? Note { get; private set; }

    public bool IsResolved => Action.HasValue;

    private Discrepancy() { }

    internal Discrepancy(string sku, DiscrepancyType type)
    {
        Sku = sku;
        Type = type;
    }

    internal void Resolve(ResolutionAction action, string? note)
    {
        Action = action;
        Note = note;
    }
}
