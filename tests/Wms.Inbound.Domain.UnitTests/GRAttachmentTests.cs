using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Inbound.Domain.UnitTests;

// What: behavioral fitness untuk aggregate GRAttachment (ADR-0015)
// Why: memverifikasi factory invariant (whitelist contentType, batas 50MB, fileName) + generasi
// blobPath berpola — byte di object storage, row hanya metadata + path.
public class GRAttachmentTests
{
    private const string Pdf = "application/pdf";
    private const long FiftyMb = 50L * 1024 * 1024;
    private static readonly Guid GrId = Guid.NewGuid();

    private static Result<GRAttachment> Create(
        string fileName = "asn.pdf", string contentType = Pdf, long sizeBytes = 1024) =>
        GRAttachment.Create(GRAttachmentId.New(), GrId, fileName, contentType, sizeBytes, DateTimeOffset.UtcNow);

    [Fact]
    public void Create_with_valid_metadata_succeeds()
    {
        var result = Create();

        Assert.True(result.IsSuccess);
        Assert.Equal(GrId, result.Value.GoodsReceiptId);
        Assert.Equal("asn.pdf", result.Value.FileName);
        Assert.Equal(1024, result.Value.SizeBytes);
    }

    [Fact]
    public void Create_generates_blob_path_pattern()
    {
        // pola {grId}/{attachmentId}/{fileName} (ADR-0015) — di-generate, bukan diterima dari luar
        var attachment = Create().Value;

        Assert.Equal($"{GrId}/{attachment.Id.Value}/asn.pdf", attachment.BlobPath);
    }

    [Fact]
    public void Create_with_blank_filename_fails()
    {
        var result = Create(fileName: "  ");

        Assert.True(result.IsFailure);
        Assert.Equal(GRAttachmentErrors.MissingFileName, result.Error);
    }

    [Fact]
    public void Create_with_filename_over_256_chars_fails()
    {
        var result = Create(fileName: new string('a', 257));

        Assert.True(result.IsFailure);
        Assert.Equal(GRAttachmentErrors.FileNameTooLong, result.Error);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/jpeg")]
    [InlineData("image/jpg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    public void Create_accepts_each_whitelisted_content_type(string contentType)
    {
        Assert.True(Create(contentType: contentType).IsSuccess);
    }

    [Fact]
    public void Create_with_content_type_outside_whitelist_fails()
    {
        var result = Create(contentType: "text/plain");

        Assert.True(result.IsFailure);
        Assert.Equal(GRAttachmentErrors.ContentTypeNotAllowed, result.Error);
    }

    [Fact]
    public void Create_with_non_positive_size_fails()
    {
        var result = Create(sizeBytes: 0);

        Assert.True(result.IsFailure);
        Assert.Equal(GRAttachmentErrors.NonPositiveSize, result.Error);
    }

    [Fact]
    public void Create_with_size_over_50mb_fails()
    {
        var result = Create(sizeBytes: FiftyMb + 1);

        Assert.True(result.IsFailure);
        Assert.Equal(GRAttachmentErrors.SizeExceedsLimit, result.Error);
    }

    [Fact]
    public void Create_with_size_exactly_50mb_succeeds()
    {
        Assert.True(Create(sizeBytes: FiftyMb).IsSuccess);
    }
}
