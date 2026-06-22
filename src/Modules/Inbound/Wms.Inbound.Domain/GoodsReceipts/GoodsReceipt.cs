using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Inbound.Domain;

// What: Aggregate Root (DDD) — GoodsReceipt, rich state machine penuh (Phase 03a)
// Why: satu-satunya pintu konsistensi penerimaan barang; invariant (warehouse, expected lines,
// two-axis discrepancy resolution ADR-0013) ditegakkan DI SINI, dan HANYA aggregate yang me-raise
// domain event (emission policy ADR-0026) — bukan service/handler. Lifecycle ubiquitous-language:
// InProgress→(scan)→Pending→(resolve)→Confirmed | Hold (overview §A).
// How: factory Create memvalidasi → Result<GoodsReceipt>; tiap transisi adalah method yang menjaga
// state legal + invariant; kegagalan bisnis = Error sebagai nilai (ADR-0019), bukan throw (FF#7).
public sealed class GoodsReceipt : AuditableAggregateRoot<GoodsReceiptId>
{
    private readonly List<ExpectedLine> _expectedLines = new();
    private readonly List<ScannedLine> _scannedLines = new();
    private readonly List<Discrepancy> _discrepancies = new();

    public string WarehouseId { get; private set; } = null!;

    public string? PoRef { get; private set; }

    public string? SupplierId { get; private set; }

    public string? DockDoor { get; private set; }

    public string? HoldReason { get; private set; }

    public GoodsReceiptStatus Status { get; private set; }

    public IReadOnlyCollection<ExpectedLine> ExpectedLines => _expectedLines.AsReadOnly();

    public IReadOnlyCollection<ScannedLine> ScannedLines => _scannedLines.AsReadOnly();

    public IReadOnlyCollection<Discrepancy> Discrepancies => _discrepancies.AsReadOnly();

    // What: quantityChecks — TURUNAN transient per SKU (tak dipersist; ADR-0013/overview)
    // Why: dihitung on-demand dari expected vs scanned (Good+QcHold) supaya tak ada state ganda yang
    // bisa drift; jadi sumber sumbu-kuantitas saat kompilasi discrepancy + bahan UI Review.
    public IReadOnlyCollection<QuantityCheck> QuantityChecks =>
        _expectedLines.Select(expected =>
        {
            var received = ReceivedQuantityFor(expected.Sku);
            var variance = received < expected.ExpectedQty ? QuantityVariance.ShortDelivery
                : received > expected.ExpectedQty ? QuantityVariance.OverDelivery
                : QuantityVariance.Normal;
            return new QuantityCheck(expected.Sku, expected.ExpectedQty, received, variance);
        }).ToList();

    private GoodsReceipt() { }

    private GoodsReceipt(
        GoodsReceiptId id, string warehouseId, string? poRef, string? supplierId, string? dockDoor)
        : base(id)
    {
        WarehouseId = warehouseId;
        PoRef = poRef;
        SupplierId = supplierId;
        DockDoor = dockDoor;
        Status = GoodsReceiptStatus.InProgress;
    }

    // What: factory + invariant guard (Result pattern, ADR-0019)
    // Why: header GR dibuka dengan expectedLines di-SNAPSHOT dari PO (sku/qty/uom) — uom dibekukan
    // agar dokumen historis stabil saat Product master berubah (ADR-0014). Sampai MasterData (04a),
    // snapshot disuplai pemanggil sebagai stand-in PO/seed.
    public static Result<GoodsReceipt> Create(
        GoodsReceiptId id,
        string warehouseId,
        IReadOnlyCollection<ExpectedLineInput> expectedLines,
        string? poRef = null,
        string? supplierId = null,
        string? dockDoor = null)
    {
        if (string.IsNullOrWhiteSpace(warehouseId))
            return Result.Failure<GoodsReceipt>(GoodsReceiptErrors.MissingWarehouse);

        if (expectedLines.Count == 0)
            return Result.Failure<GoodsReceipt>(GoodsReceiptErrors.NoExpectedLines);

        foreach (var line in expectedLines)
        {
            if (string.IsNullOrWhiteSpace(line.Sku))
                return Result.Failure<GoodsReceipt>(GoodsReceiptErrors.MissingSku);
            if (line.ExpectedQty <= 0)
                return Result.Failure<GoodsReceipt>(GoodsReceiptErrors.NonPositiveExpectedQuantity);
            if (string.IsNullOrWhiteSpace(line.Uom))
                return Result.Failure<GoodsReceipt>(GoodsReceiptErrors.MissingUom);
        }

        var goodsReceipt = new GoodsReceipt(id, warehouseId, poRef, supplierId, dockDoor);
        foreach (var line in expectedLines)
            goodsReceipt._expectedLines.Add(new ExpectedLine(line.Sku, line.ExpectedQty, line.Uom));

        return Result.Success(goodsReceipt);
    }

    // What: transisi InProgress (tetap) — append hasil scan (overview §A2)
    // Why: scan bisa multi-session; tiap entry capture qty fisik + kondisi (lineStatus) + batch/
    // expiry. Hanya legal saat InProgress. Validasi bentuk minimal; sumbu discrepancy dihitung
    // nanti saat DeclareScanComplete.
    public Result ScanItem(string sku, int actualQty, string? batch, DateOnly? expiry, LineStatus lineStatus)
    {
        if (Status != GoodsReceiptStatus.InProgress)
            return Result.Failure(GoodsReceiptErrors.NotInProgress);
        if (string.IsNullOrWhiteSpace(sku))
            return Result.Failure(GoodsReceiptErrors.MissingSku);
        if (actualQty <= 0)
            return Result.Failure(GoodsReceiptErrors.NonPositiveScanQuantity);

        _scannedLines.Add(new ScannedLine(sku, actualQty, batch, expiry, lineStatus));
        return Result.Success();
    }

    // What: transisi InProgress→Pending — kompilasi discrepancies dua-sumbu (ADR-0013, §A3)
    // Why: operator declare "scan selesai"; sistem auto-hitung sumbu KUANTITAS (variance per SKU)
    // dan sumbu KONDISI (lineStatus≠Good) → tiap pelanggaran jadi entry discrepancy TERPISAH. Satu
    // SKU bisa memunculkan >1 entry (mis. OverDelivery + QcHold) = dua keputusan resolusi berbeda.
    public Result DeclareScanComplete()
    {
        if (Status != GoodsReceiptStatus.InProgress)
            return Result.Failure(GoodsReceiptErrors.NotInProgress);

        _discrepancies.Clear();

        // sumbu KUANTITAS: per expected line, variance ≠ Normal → satu entry
        foreach (var check in QuantityChecks)
        {
            if (check.Variance == QuantityVariance.ShortDelivery)
                _discrepancies.Add(new Discrepancy(check.Sku, DiscrepancyType.ShortDelivery));
            else if (check.Variance == QuantityVariance.OverDelivery)
                _discrepancies.Add(new Discrepancy(check.Sku, DiscrepancyType.OverDelivery));
        }

        // sumbu KONDISI: per SKU dgn line QcHold / WrongItem → satu entry (independen dari kuantitas)
        foreach (var sku in DistinctSkusWith(LineStatus.QcHold))
            _discrepancies.Add(new Discrepancy(sku, DiscrepancyType.QcHold));
        foreach (var sku in DistinctSkusWith(LineStatus.WrongItem))
            _discrepancies.Add(new Discrepancy(sku, DiscrepancyType.WrongItem));

        Status = GoodsReceiptStatus.Pending;
        return Result.Success();
    }

    // What: set resolusi SPV atas satu discrepancy (Pending) — pasangan dari invariant Confirm
    // Why: tiap discrepancy butuh keputusan (action) sebelum GR boleh Confirm; action menentukan
    // turunan received/rejected. SPV bisa pilih non-default; default disediakan ApplyDefaultResolutions.
    public Result ResolveDiscrepancy(
        string sku, DiscrepancyType type, ResolutionAction action, string? note = null)
    {
        if (Status != GoodsReceiptStatus.Pending)
            return Result.Failure(GoodsReceiptErrors.NotPending);

        var discrepancy = _discrepancies.FirstOrDefault(entry => entry.Sku == sku && entry.Type == type);
        if (discrepancy is null)
            return Result.Failure(GoodsReceiptErrors.DiscrepancyNotFound);

        discrepancy.Resolve(action, note);
        return Result.Success();
    }

    // What: convenience — resolve semua discrepancy yang belum ter-resolve dgn default SOP (§A4)
    // Why: jalur umum "SPV setuju default" tanpa memanggil ResolveDiscrepancy satu per satu.
    public Result ApplyDefaultResolutions()
    {
        if (Status != GoodsReceiptStatus.Pending)
            return Result.Failure(GoodsReceiptErrors.NotPending);

        foreach (var discrepancy in _discrepancies.Where(entry => !entry.IsResolved))
            discrepancy.Resolve(DefaultActionFor(discrepancy.Type), note: null);
        return Result.Success();
    }

    // What: transisi Pending→Confirmed + emission (ADR-0026) — terminal, read-only
    // Why: enforce invariant two-axis (ADR-0013) "semua discrepancy ter-resolve" SEBELUM emit; lalu
    // turunkan receivedLines/rejectedLines dari resolusi dan raise GoodsReceiptConfirmed. Event hanya
    // di-raise pada fakta sukses — invariant gagal = Conflict tanpa emit.
    public Result Confirm()
    {
        if (Status != GoodsReceiptStatus.Pending)
            return Result.Failure(GoodsReceiptErrors.NotPending);
        if (_discrepancies.Any(discrepancy => !discrepancy.IsResolved))
            return Result.Failure(GoodsReceiptErrors.UnresolvedDiscrepancy);

        var (received, rejected) = BuildConfirmationOutcome();
        Status = GoodsReceiptStatus.Confirmed;
        RaiseDomainEvent(new GoodsReceiptConfirmed(Id, WarehouseId, SupplierId, received, rejected));
        return Result.Success();
    }

    // What: transisi Pending→Hold (terminal di scope ini) — TIDAK emit event (overview §A4)
    // Why: SPV reject seluruh GR (alasan berat); Inbound tak memancarkan apa pun. What-happens-after-
    // Hold di luar scope (gap, jangan dibangun). Validasi non-empty reason = input-shape di validator.
    public Result Hold(string reason)
    {
        if (Status != GoodsReceiptStatus.Pending)
            return Result.Failure(GoodsReceiptErrors.NotPending);

        HoldReason = reason;
        Status = GoodsReceiptStatus.Hold;
        return Result.Success();
    }

    // What: derive receivedLines/rejectedLines dari scannedLines + resolusi (turunan two-axis)
    // Why: payload GRConfirmed adalah KONSEKUENSI keputusan resolusi, bukan input terpisah — Good/
    // QcHold → received (status di-bawa untuk OnHand vs Quarantine di Inventory); WrongItem → rejected
    // (ReturnToSupplier); OverDelivery yg di-RejectExcess → excess di-trim ke rejected dengan QcHold
    // DIPERTAHANKAN (ke QC) dan Good di-trim lebih dulu.
    private (List<ConfirmedReceivedLine> Received, List<ConfirmedRejectedLine> Rejected) BuildConfirmationOutcome()
    {
        var received = new List<ConfirmedReceivedLine>();
        var rejected = new List<ConfirmedRejectedLine>();

        // sumbu KONDISI: WrongItem → rejected (ReturnToSupplier), per scanned line
        foreach (var line in _scannedLines.Where(entry => entry.LineStatus == LineStatus.WrongItem))
            rejected.Add(new ConfirmedRejectedLine(line.Sku, line.ActualQty, RejectionReason.ReturnToSupplier));

        foreach (var expected in _expectedLines)
        {
            // QcHold lebih dulu (dipertahankan ke QC), lalu Good (di-trim saat over-delivery)
            var keepOrder = _scannedLines
                .Where(entry => entry.Sku == expected.Sku && entry.LineStatus == LineStatus.QcHold)
                .Concat(_scannedLines.Where(entry => entry.Sku == expected.Sku && entry.LineStatus == LineStatus.Good))
                .ToList();
            var receivedQty = keepOrder.Sum(entry => entry.ActualQty);

            var trimExcess = receivedQty > expected.ExpectedQty
                && _discrepancies.Any(d => d.Sku == expected.Sku
                    && d.Type == DiscrepancyType.OverDelivery
                    && d.Action == ResolutionAction.RejectExcess);

            if (trimExcess)
            {
                var cap = expected.ExpectedQty;
                foreach (var line in keepOrder)
                {
                    if (cap <= 0) break;
                    var take = Math.Min(line.ActualQty, cap);
                    received.Add(new ConfirmedReceivedLine(line.Sku, take, line.LineStatus, line.Batch, line.Expiry));
                    cap -= take;
                }
                rejected.Add(new ConfirmedRejectedLine(
                    expected.Sku, receivedQty - expected.ExpectedQty, RejectionReason.RejectExcess));
            }
            else
            {
                foreach (var line in keepOrder)
                    received.Add(new ConfirmedReceivedLine(
                        line.Sku, line.ActualQty, line.LineStatus, line.Batch, line.Expiry));
            }
        }

        return (received, rejected);
    }

    // What: default ResolutionAction per type sesuai SOP (overview §A4)
    private static ResolutionAction DefaultActionFor(DiscrepancyType type) => type switch
    {
        DiscrepancyType.ShortDelivery => ResolutionAction.AcceptPartial,
        DiscrepancyType.OverDelivery => ResolutionAction.RejectExcess,
        DiscrepancyType.WrongItem => ResolutionAction.ReturnToSupplier,
        DiscrepancyType.QcHold => ResolutionAction.SendToQC,
        _ => ResolutionAction.AcceptPartial   // unreachable: 4 nilai enum sudah dicakup di atas
    };

    // What: kuantitas "diterima sebagai SKU benar" = Good + QcHold (WrongItem dikecualikan dari variance)
    private int ReceivedQuantityFor(string sku) =>
        _scannedLines
            .Where(line => line.Sku == sku && line.LineStatus is LineStatus.Good or LineStatus.QcHold)
            .Sum(line => line.ActualQty);

    private IEnumerable<string> DistinctSkusWith(LineStatus lineStatus) =>
        _scannedLines.Where(line => line.LineStatus == lineStatus).Select(line => line.Sku).Distinct();
}
