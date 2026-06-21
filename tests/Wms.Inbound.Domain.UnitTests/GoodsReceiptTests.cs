namespace Wms.Inbound.Domain.UnitTests;

// What: behavioral fitness untuk aggregate GoodsReceipt — rich state machine (Phase 03a)
// Why: memverifikasi invariant factory + emission policy (ADR-0026) + transisi state legal/ilegal
// + two-axis discrepancy (ADR-0013) — dimensi yang tak terjangkau NetArchTest statik. Test = spec
// jujur dari ubiquitous language: InProgress→(scan)→Pending→(resolve)→Confirmed/Hold.
public class GoodsReceiptTests
{
    private static readonly ExpectedLineInput[] OneExpected = [new("SKU-1", 10, "carton")];

    private static GoodsReceipt NewInProgress(params ExpectedLineInput[] expected) =>
        GoodsReceipt.Create(GoodsReceiptId.New(), "WH-JKT", expected.Length == 0 ? OneExpected : expected).Value;

    // --- Create / InProgress ---

    [Fact]
    public void Create_with_valid_expected_lines_succeeds_and_is_in_progress()
    {
        var result = GoodsReceipt.Create(GoodsReceiptId.New(), "WH-JKT", OneExpected);

        Assert.True(result.IsSuccess);
        Assert.Equal(GoodsReceiptStatus.InProgress, result.Value.Status);
        Assert.Single(result.Value.ExpectedLines);
    }

    [Fact]
    public void Create_snapshots_expected_line_sku_qty_and_uom()
    {
        // snapshot field master-data kritikal (uom) ke aggregate (ADR-0014)
        var gr = GoodsReceipt.Create(
            GoodsReceiptId.New(), "WH-JKT", [new("SKU-9", 42, "pallet")]).Value;

        var line = Assert.Single(gr.ExpectedLines);
        Assert.Equal("SKU-9", line.Sku);
        Assert.Equal(42, line.ExpectedQty);
        Assert.Equal("pallet", line.Uom);
    }

    [Fact]
    public void Create_without_expected_lines_fails()
    {
        var result = GoodsReceipt.Create(GoodsReceiptId.New(), "WH-JKT", []);

        Assert.True(result.IsFailure);
        Assert.Equal(GoodsReceiptErrors.NoExpectedLines, result.Error);
    }

    [Fact]
    public void Create_with_blank_warehouse_fails()
    {
        var result = GoodsReceipt.Create(GoodsReceiptId.New(), "  ", OneExpected);

        Assert.True(result.IsFailure);
        Assert.Equal(GoodsReceiptErrors.MissingWarehouse, result.Error);
    }

    [Fact]
    public void Create_with_blank_sku_fails()
    {
        var result = GoodsReceipt.Create(GoodsReceiptId.New(), "WH-JKT", [new(" ", 10, "carton")]);

        Assert.True(result.IsFailure);
        Assert.Equal(GoodsReceiptErrors.MissingSku, result.Error);
    }

    [Fact]
    public void Create_with_non_positive_expected_qty_fails()
    {
        var result = GoodsReceipt.Create(GoodsReceiptId.New(), "WH-JKT", [new("SKU-1", 0, "carton")]);

        Assert.True(result.IsFailure);
        Assert.Equal(GoodsReceiptErrors.NonPositiveExpectedQuantity, result.Error);
    }

    [Fact]
    public void Create_with_blank_uom_fails()
    {
        var result = GoodsReceipt.Create(GoodsReceiptId.New(), "WH-JKT", [new("SKU-1", 10, " ")]);

        Assert.True(result.IsFailure);
        Assert.Equal(GoodsReceiptErrors.MissingUom, result.Error);
    }

    [Fact]
    public void Create_does_not_raise_domain_event()
    {
        // emission policy: pembuatan bukan "fakta bisnis" yang dipublish (ADR-0026)
        Assert.Empty(NewInProgress().DomainEvents);
    }

    // --- ScanItem / state machine ---

    [Fact]
    public void Scan_item_in_progress_appends_scanned_line_and_stays_in_progress()
    {
        var gr = NewInProgress();

        var result = gr.ScanItem("SKU-1", 4, "B-1", new DateOnly(2027, 1, 1), LineStatus.Good);

        Assert.True(result.IsSuccess);
        Assert.Equal(GoodsReceiptStatus.InProgress, gr.Status);
        var line = Assert.Single(gr.ScannedLines);
        Assert.Equal("SKU-1", line.Sku);
        Assert.Equal(4, line.ActualQty);
        Assert.Equal("B-1", line.Batch);
        Assert.Equal(LineStatus.Good, line.LineStatus);
    }

    [Fact]
    public void Scan_item_with_non_positive_qty_fails()
    {
        var gr = NewInProgress();

        var result = gr.ScanItem("SKU-1", 0, null, null, LineStatus.Good);

        Assert.True(result.IsFailure);
        Assert.Equal(GoodsReceiptErrors.NonPositiveScanQuantity, result.Error);
    }

    [Fact]
    public void Scan_item_after_scan_complete_is_illegal()
    {
        var gr = NewInProgress();
        gr.ScanItem("SKU-1", 10, null, null, LineStatus.Good);
        gr.DeclareScanComplete();

        var result = gr.ScanItem("SKU-1", 1, null, null, LineStatus.Good);

        Assert.True(result.IsFailure);
        Assert.Equal(GoodsReceiptErrors.NotInProgress, result.Error);
    }

    // --- DeclareScanComplete / two-axis discrepancy (ADR-0013) ---

    [Fact]
    public void Declare_scan_complete_transitions_to_pending()
    {
        var gr = NewInProgress();
        gr.ScanItem("SKU-1", 10, null, null, LineStatus.Good);

        var result = gr.DeclareScanComplete();

        Assert.True(result.IsSuccess);
        Assert.Equal(GoodsReceiptStatus.Pending, gr.Status);
    }

    [Fact]
    public void Declare_scan_complete_twice_is_illegal()
    {
        var gr = NewInProgress();
        gr.ScanItem("SKU-1", 10, null, null, LineStatus.Good);
        gr.DeclareScanComplete();

        var result = gr.DeclareScanComplete();

        Assert.True(result.IsFailure);
        Assert.Equal(GoodsReceiptErrors.NotInProgress, result.Error);
    }

    [Fact]
    public void Matching_good_quantity_yields_no_discrepancy()
    {
        var gr = NewInProgress(new ExpectedLineInput("SKU-1", 10, "carton"));
        gr.ScanItem("SKU-1", 10, null, null, LineStatus.Good);

        gr.DeclareScanComplete();

        Assert.Empty(gr.Discrepancies);
    }

    [Fact]
    public void Short_delivery_yields_one_short_discrepancy()
    {
        var gr = NewInProgress(new ExpectedLineInput("SKU-1", 10, "carton"));
        gr.ScanItem("SKU-1", 6, null, null, LineStatus.Good);

        gr.DeclareScanComplete();

        var discrepancy = Assert.Single(gr.Discrepancies);
        Assert.Equal("SKU-1", discrepancy.Sku);
        Assert.Equal(DiscrepancyType.ShortDelivery, discrepancy.Type);
        Assert.False(discrepancy.IsResolved);
    }

    [Fact]
    public void Over_delivery_yields_one_over_discrepancy()
    {
        var gr = NewInProgress(new ExpectedLineInput("SKU-1", 10, "carton"));
        gr.ScanItem("SKU-1", 13, null, null, LineStatus.Good);

        gr.DeclareScanComplete();

        var discrepancy = Assert.Single(gr.Discrepancies);
        Assert.Equal(DiscrepancyType.OverDelivery, discrepancy.Type);
    }

    [Fact]
    public void Qc_hold_line_yields_one_qc_hold_discrepancy()
    {
        var gr = NewInProgress(new ExpectedLineInput("SKU-1", 10, "carton"));
        gr.ScanItem("SKU-1", 10, null, null, LineStatus.QcHold);

        gr.DeclareScanComplete();

        var discrepancy = Assert.Single(gr.Discrepancies);
        Assert.Equal(DiscrepancyType.QcHold, discrepancy.Type);
    }

    [Fact]
    public void Wrong_item_line_yields_one_wrong_item_discrepancy()
    {
        // WrongItem tak dihitung ke variance: Good 10 = expected 10 → Normal; satu entry WrongItem
        var gr = NewInProgress(new ExpectedLineInput("SKU-1", 10, "carton"));
        gr.ScanItem("SKU-1", 10, null, null, LineStatus.Good);
        gr.ScanItem("SKU-1", 2, null, null, LineStatus.WrongItem);

        gr.DeclareScanComplete();

        var discrepancy = Assert.Single(gr.Discrepancies);
        Assert.Equal(DiscrepancyType.WrongItem, discrepancy.Type);
    }

    [Fact]
    public void Over_delivery_with_qc_hold_yields_two_independent_discrepancies()
    {
        // ADR-0013: satu SKU kena DUA sumbu → DUA entry terpisah (qty-axis OverDelivery + status-axis QcHold)
        var gr = NewInProgress(new ExpectedLineInput("SKU-1", 10, "carton"));
        gr.ScanItem("SKU-1", 10, null, null, LineStatus.Good);
        gr.ScanItem("SKU-1", 2, null, null, LineStatus.QcHold);   // received 12 > expected 10 → over; ada QcHold

        gr.DeclareScanComplete();

        Assert.Equal(2, gr.Discrepancies.Count);
        Assert.Contains(gr.Discrepancies, d => d.Type == DiscrepancyType.OverDelivery);
        Assert.Contains(gr.Discrepancies, d => d.Type == DiscrepancyType.QcHold);
    }

    [Fact]
    public void Quantity_checks_compute_received_and_variance()
    {
        var gr = NewInProgress(new ExpectedLineInput("SKU-1", 10, "carton"));
        gr.ScanItem("SKU-1", 7, null, null, LineStatus.Good);

        gr.DeclareScanComplete();

        var check = Assert.Single(gr.QuantityChecks);
        Assert.Equal("SKU-1", check.Sku);
        Assert.Equal(10, check.ExpectedQty);
        Assert.Equal(7, check.ReceivedQty);
        Assert.Equal(QuantityVariance.ShortDelivery, check.Variance);
    }

    // --- ResolveDiscrepancy ---

    [Fact]
    public void Resolve_sets_action_on_matching_discrepancy()
    {
        var gr = PendingShort();

        var result = gr.ResolveDiscrepancy("SKU-1", DiscrepancyType.ShortDelivery, ResolutionAction.AcceptPartial);

        Assert.True(result.IsSuccess);
        Assert.True(Assert.Single(gr.Discrepancies).IsResolved);
    }

    [Fact]
    public void Resolve_unknown_discrepancy_fails()
    {
        var gr = PendingShort();

        var result = gr.ResolveDiscrepancy("SKU-1", DiscrepancyType.OverDelivery, ResolutionAction.RejectExcess);

        Assert.True(result.IsFailure);
        Assert.Equal(GoodsReceiptErrors.DiscrepancyNotFound, result.Error);
    }

    [Fact]
    public void Apply_default_resolutions_resolves_per_type()
    {
        var gr = PendingShort();

        gr.ApplyDefaultResolutions();

        Assert.Equal(ResolutionAction.AcceptPartial, Assert.Single(gr.Discrepancies).Action);
    }

    // --- Confirm: invariant resolution-required + emission policy ---

    [Fact]
    public void Confirm_with_no_discrepancies_transitions_and_raises_event()
    {
        var gr = PendingClean();

        var result = gr.Confirm();

        Assert.True(result.IsSuccess);
        Assert.Equal(GoodsReceiptStatus.Confirmed, gr.Status);
        Assert.Single(gr.DomainEvents.OfType<GoodsReceiptConfirmed>());
    }

    [Fact]
    public void Confirm_with_unresolved_discrepancy_fails_and_raises_no_event()
    {
        // invariant ADR-0013: tiap discrepancy WAJIB punya resolution sebelum Confirm
        var gr = PendingShort();

        var result = gr.Confirm();

        Assert.True(result.IsFailure);
        Assert.Equal(GoodsReceiptErrors.UnresolvedDiscrepancy, result.Error);
        Assert.Empty(gr.DomainEvents);
        Assert.Equal(GoodsReceiptStatus.Pending, gr.Status);
    }

    [Fact]
    public void Confirm_from_in_progress_is_illegal()
    {
        var result = NewInProgress().Confirm();

        Assert.True(result.IsFailure);
        Assert.Equal(GoodsReceiptErrors.NotPending, result.Error);
    }

    [Fact]
    public void Declare_scan_complete_does_not_raise_event()
    {
        // emission policy: hanya Confirm yang me-raise event (ADR-0026)
        Assert.Empty(PendingClean().DomainEvents);
    }

    // --- Confirm: derive receivedLines / rejectedLines (two-axis turunan resolusi) ---

    [Fact]
    public void Confirm_derives_good_received_line()
    {
        var gr = PendingClean();

        gr.Confirm();

        var confirmed = gr.DomainEvents.OfType<GoodsReceiptConfirmed>().Single();
        var line = Assert.Single(confirmed.ReceivedLines);
        Assert.Equal("SKU-1", line.Sku);
        Assert.Equal(10, line.Quantity);
        Assert.Equal(LineStatus.Good, line.Status);
        Assert.Empty(confirmed.RejectedLines);
    }

    [Fact]
    public void Confirm_derives_qc_hold_received_line()
    {
        var gr = Pending(new ExpectedLineInput("SKU-1", 10, "carton"), ("SKU-1", 10, LineStatus.QcHold));
        gr.ApplyDefaultResolutions();   // QcHold → SendToQC

        gr.Confirm();

        var line = Assert.Single(gr.DomainEvents.OfType<GoodsReceiptConfirmed>().Single().ReceivedLines);
        Assert.Equal(LineStatus.QcHold, line.Status);
        Assert.Equal(10, line.Quantity);
    }

    [Fact]
    public void Confirm_derives_wrong_item_as_rejected_return_to_supplier()
    {
        var gr = Pending(new ExpectedLineInput("SKU-1", 10, "carton"),
            ("SKU-1", 10, LineStatus.Good), ("SKU-1", 2, LineStatus.WrongItem));
        gr.ApplyDefaultResolutions();

        gr.Confirm();

        var confirmed = gr.DomainEvents.OfType<GoodsReceiptConfirmed>().Single();
        Assert.Equal(10, Assert.Single(confirmed.ReceivedLines).Quantity);
        var rejected = Assert.Single(confirmed.RejectedLines);
        Assert.Equal(2, rejected.Quantity);
        Assert.Equal(RejectionReason.ReturnToSupplier, rejected.Reason);
    }

    [Fact]
    public void Confirm_over_delivery_rejects_excess()
    {
        var gr = Pending(new ExpectedLineInput("SKU-1", 10, "carton"), ("SKU-1", 13, LineStatus.Good));
        gr.ApplyDefaultResolutions();   // OverDelivery → RejectExcess

        gr.Confirm();

        var confirmed = gr.DomainEvents.OfType<GoodsReceiptConfirmed>().Single();
        Assert.Equal(10, Assert.Single(confirmed.ReceivedLines).Quantity);   // capped ke expected
        var rejected = Assert.Single(confirmed.RejectedLines);
        Assert.Equal(3, rejected.Quantity);
        Assert.Equal(RejectionReason.RejectExcess, rejected.Reason);
    }

    [Fact]
    public void Confirm_short_delivery_accepts_partial_without_rejection()
    {
        var gr = PendingShort();
        gr.ApplyDefaultResolutions();   // ShortDelivery → AcceptPartial

        gr.Confirm();

        var confirmed = gr.DomainEvents.OfType<GoodsReceiptConfirmed>().Single();
        Assert.Equal(6, Assert.Single(confirmed.ReceivedLines).Quantity);
        Assert.Empty(confirmed.RejectedLines);
    }

    [Fact]
    public void Confirm_over_with_qc_hold_preserves_qc_hold_and_trims_good()
    {
        // two-axis (ADR-0013): received 12 (Good 10 + QcHold 2), expected 10 → QcHold dipertahankan
        // ke QC (2), Good di-trim ke 8, excess 2 → rejected RejectExcess.
        var gr = Pending(new ExpectedLineInput("SKU-1", 10, "carton"),
            ("SKU-1", 10, LineStatus.Good), ("SKU-1", 2, LineStatus.QcHold));
        gr.ApplyDefaultResolutions();

        gr.Confirm();

        var confirmed = gr.DomainEvents.OfType<GoodsReceiptConfirmed>().Single();
        Assert.Equal(2, confirmed.ReceivedLines.Single(l => l.Status == LineStatus.QcHold).Quantity);
        Assert.Equal(8, confirmed.ReceivedLines.Single(l => l.Status == LineStatus.Good).Quantity);
        var rejected = Assert.Single(confirmed.RejectedLines);
        Assert.Equal(2, rejected.Quantity);
        Assert.Equal(RejectionReason.RejectExcess, rejected.Reason);
    }

    // --- Hold ---

    [Fact]
    public void Hold_from_pending_transitions_to_hold_without_event()
    {
        var gr = PendingShort();

        var result = gr.Hold("dokumen bermasalah");

        Assert.True(result.IsSuccess);
        Assert.Equal(GoodsReceiptStatus.Hold, gr.Status);
        Assert.Equal("dokumen bermasalah", gr.HoldReason);
        Assert.Empty(gr.DomainEvents);
    }

    [Fact]
    public void Hold_from_in_progress_is_illegal()
    {
        var result = NewInProgress().Hold("x");

        Assert.True(result.IsFailure);
        Assert.Equal(GoodsReceiptErrors.NotPending, result.Error);
    }

    // --- test helpers: capai state Pending lewat flow penuh ---

    private static GoodsReceipt Pending(
        ExpectedLineInput expected, params (string Sku, int Qty, LineStatus Status)[] scans)
    {
        var gr = GoodsReceipt.Create(GoodsReceiptId.New(), "WH-JKT", [expected]).Value;
        foreach (var scan in scans)
            gr.ScanItem(scan.Sku, scan.Qty, null, null, scan.Status);
        gr.DeclareScanComplete();
        return gr;
    }

    private static GoodsReceipt PendingClean() =>
        Pending(new ExpectedLineInput("SKU-1", 10, "carton"), ("SKU-1", 10, LineStatus.Good));

    private static GoodsReceipt PendingShort() =>
        Pending(new ExpectedLineInput("SKU-1", 10, "carton"), ("SKU-1", 6, LineStatus.Good));
}
