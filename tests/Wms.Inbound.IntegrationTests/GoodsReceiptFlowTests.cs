using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Storage;
using Wms.BuildingBlocks.Infrastructure.Messaging;
using Wms.Inbound.Application.DependencyInjection;
using Wms.Inbound.Application.Features.ConfirmGoodsReceipt;
using Wms.Inbound.Application.Features.CreateGoodsReceipt;
using Wms.Inbound.Application.Features.DeclareScanComplete;
using Wms.Inbound.Application.Features.ResolveDiscrepancy;
using Wms.Inbound.Application.Features.ScanItem;
using Wms.Inbound.Application.Features.UploadAttachment;
using Wms.Inbound.Contracts;
using Wms.Inbound.Domain;
using Wms.Inbound.Infrastructure.DependencyInjection;
using Wms.Inbound.Infrastructure.Persistence;
using Wms.Platform.Local.DependencyInjection;
using Wms.TestSupport;

namespace Wms.Inbound.IntegrationTests;

// What: integration test flow GoodsReceipt 03a (DoD Phase 03a) atas Postgres nyata
// Why: membuktikan state machine penuh lewat pipeline REAL (Create→Scan→Declare→Resolve→Confirm)
// menghasilkan GRConfirmedV1 dgn receivedLines (Good/QcHold) + rejectedLines (ReturnToSupplier/
// RejectExcess) yang BENAR (turunan two-axis ADR-0013), ditulis ke Outbox dalam satu transaksi; dan
// UploadAttachment menulis byte ke object storage + metadata ke row (urutan disiplin, ADR-0015).
[Collection(PostgresCollection.Name)]
public sealed class GoodsReceiptFlowTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Full_flow_confirm_derives_received_and_rejected_lines_to_outbox()
    {
        var (provider, _) = await BuildAsync();
        await using var _provider = provider;

        // expected: SKU-A 10, SKU-B 10
        var grId = (await SendAsync(provider, new CreateGoodsReceiptCommand("WH-JKT",
        [
            new CreateGoodsReceiptLine("SKU-A", 10, "carton"),
            new CreateGoodsReceiptLine("SKU-B", 10, "carton"),
        ]))).Value;

        // SKU-A bersih (Good 10). SKU-B: Good 10 + QcHold 2 + WrongItem 1 → over (12>10) + QcHold + WrongItem
        Assert.True((await SendAsync(provider, new ScanItemCommand(grId, "SKU-A", 10, null, null, LineStatus.Good))).IsSuccess);
        Assert.True((await SendAsync(provider, new ScanItemCommand(grId, "SKU-B", 10, "B-1", null, LineStatus.Good))).IsSuccess);
        Assert.True((await SendAsync(provider, new ScanItemCommand(grId, "SKU-B", 2, "B-1", null, LineStatus.QcHold))).IsSuccess);
        Assert.True((await SendAsync(provider, new ScanItemCommand(grId, "SKU-B", 1, null, null, LineStatus.WrongItem))).IsSuccess);
        Assert.True((await SendAsync(provider, new DeclareScanCompleteCommand(grId))).IsSuccess);

        // resolve ketiga discrepancy SKU-B dengan default SOP (overview §A4)
        Assert.True((await SendAsync(provider, new ResolveDiscrepancyCommand(grId, "SKU-B", DiscrepancyType.OverDelivery, ResolutionAction.RejectExcess))).IsSuccess);
        Assert.True((await SendAsync(provider, new ResolveDiscrepancyCommand(grId, "SKU-B", DiscrepancyType.QcHold, ResolutionAction.SendToQC))).IsSuccess);
        Assert.True((await SendAsync(provider, new ResolveDiscrepancyCommand(grId, "SKU-B", DiscrepancyType.WrongItem, ResolutionAction.ReturnToSupplier))).IsSuccess);

        Assert.True((await SendAsync(provider, new ConfirmGoodsReceiptCommand(grId))).IsSuccess);

        var payload = await ReadOutboxPayloadAsync(provider);

        // receivedLines: SKU-A Good 10; SKU-B QcHold 2 (dipertahankan ke QC); SKU-B Good 8 (di-trim)
        Assert.Equal(3, payload.ReceivedLines.Count);
        Assert.Contains(payload.ReceivedLines, line => line is { Sku: "SKU-A", Quantity: 10, Status: "Good" });
        Assert.Contains(payload.ReceivedLines, line => line is { Sku: "SKU-B", Quantity: 8, Status: "Good" });
        Assert.Contains(payload.ReceivedLines, line => line is { Sku: "SKU-B", Quantity: 2, Status: "QcHold" });

        // rejectedLines: SKU-B excess 2 (RejectExcess) + WrongItem 1 (ReturnToSupplier)
        Assert.Equal(2, payload.RejectedLines.Count);
        Assert.Contains(payload.RejectedLines, line => line is { Sku: "SKU-B", Quantity: 2, Reason: "RejectExcess" });
        Assert.Contains(payload.RejectedLines, line => line is { Sku: "SKU-B", Quantity: 1, Reason: "ReturnToSupplier" });
    }

    [Fact]
    public async Task Upload_attachment_writes_blob_to_object_store_and_metadata_row()
    {
        var (provider, _) = await BuildAsync();
        await using var _provider = provider;

        var goodsReceiptId = Guid.NewGuid();   // logical FK (ADR-0015) — tak butuh GR ada
        var bytes = new byte[] { 10, 20, 30, 40, 50 };

        var attachmentId = (await SendAsync(provider, new UploadAttachmentCommand(
            goodsReceiptId, "asn.pdf", "application/pdf", bytes.Length, new MemoryStream(bytes)))).Value;

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InboundDbContext>();
        var row = await db.Attachments.SingleAsync();
        Assert.Equal(goodsReceiptId, row.GoodsReceiptId);
        Assert.Equal("asn.pdf", row.FileName);
        Assert.Equal(bytes.Length, row.SizeBytes);
        Assert.Equal($"{goodsReceiptId}/{attachmentId}/asn.pdf", row.BlobPath);

        // byte BENAR-BENAR ada di object storage pada blobPath
        var store = scope.ServiceProvider.GetRequiredService<IObjectStore>();
        await using var blob = await store.GetAsync(row.BlobPath);
        Assert.NotNull(blob);
        using var buffer = new MemoryStream();
        await blob!.CopyToAsync(buffer);
        Assert.Equal(bytes, buffer.ToArray());
    }

    [Fact]
    public async Task Upload_attachment_with_bad_content_type_fails_and_writes_no_row()
    {
        var (provider, _) = await BuildAsync();
        await using var _provider = provider;

        var result = await SendAsync(provider, new UploadAttachmentCommand(
            Guid.NewGuid(), "virus.exe", "application/octet-stream", 1024, new MemoryStream([1, 2, 3])));

        Assert.True(result.IsFailure);
        Assert.Equal("gr_attachment.content_type_not_allowed", result.Error.Code);

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InboundDbContext>();
        Assert.Equal(0, await db.Attachments.CountAsync());   // tak ada row dari attempt yang ditolak
    }

    // provider mirror host Inbound: pipeline + infrastruktur modul + adapter Local (audit + object store)
    private async Task<(ServiceProvider Provider, string ObjectRoot)> BuildAsync()
    {
        var objectRoot = Path.Combine(Path.GetTempPath(), "wms-inbound-obj-" + Guid.NewGuid().ToString("N"));
        var provider = new ServiceCollection()
            .AddLogging()
            .AddInboundApplication()
            .AddInboundInfrastructure(await fixture.CreateDatabaseAsync())
            .AddLocalMessaging()
            .AddLocalAuditing()
            .AddLocalObjectStore(objectRoot)
            .BuildServiceProvider();

        using (var scope = provider.CreateScope())
            await scope.ServiceProvider.GetRequiredService<InboundDbContext>().Database.EnsureCreatedAsync();
        return (provider, objectRoot);
    }

    private static async Task<TResponse> SendAsync<TResponse>(IServiceProvider provider, IRequest<TResponse> request)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private static async Task<GRConfirmedV1> ReadOutboxPayloadAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InboundDbContext>();
        var outbox = await db.Set<OutboxMessage>().SingleAsync();
        Assert.Equal(GRConfirmedV1.LogicalName, outbox.LogicalName);
        return JsonSerializer.Deserialize<GRConfirmedV1>(outbox.Payload)!;
    }
}
