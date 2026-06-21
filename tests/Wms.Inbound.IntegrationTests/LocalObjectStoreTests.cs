using Wms.BuildingBlocks.Application.Storage;
using Wms.Platform.Local.Storage;

namespace Wms.Inbound.IntegrationTests;

// What: round-trip test adapter LocalObjectStore (port IObjectStore, ADR-0015)
// Why: membuktikan kontrak port di backend filesystem — Put→Get mengembalikan byte identik,
// Get blob absen = null (no-throw), Delete menghapus. Tanpa Postgres (murni filesystem).
public sealed class LocalObjectStoreTests
{
    private static string NewRoot() =>
        Path.Combine(Path.GetTempPath(), "wms-objstore-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Put_then_get_round_trips_bytes()
    {
        var root = NewRoot();
        try
        {
            IObjectStore store = new LocalObjectStore(root);
            var bytes = new byte[] { 1, 2, 3, 4, 5 };

            await store.PutAsync("gr-1/att-1/asn.pdf", new MemoryStream(bytes));

            await using var got = await store.GetAsync("gr-1/att-1/asn.pdf");
            Assert.NotNull(got);
            using var buffer = new MemoryStream();
            await got!.CopyToAsync(buffer);
            Assert.Equal(bytes, buffer.ToArray());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Get_missing_blob_returns_null()
    {
        IObjectStore store = new LocalObjectStore(NewRoot());

        Assert.Null(await store.GetAsync("nope/x/y.pdf"));
    }

    [Fact]
    public async Task Delete_removes_blob()
    {
        var root = NewRoot();
        try
        {
            IObjectStore store = new LocalObjectStore(root);
            await store.PutAsync("a/b/c.pdf", new MemoryStream([9]));

            await store.DeleteAsync("a/b/c.pdf");

            Assert.Null(await store.GetAsync("a/b/c.pdf"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
