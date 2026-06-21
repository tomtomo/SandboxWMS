using Wms.BuildingBlocks.Application.Auditing;

namespace Wms.BuildingBlocks.UnitTests.Auditing;

// What: unit test predikat PII-redaction (ADR-0022)
// Why: audit payload TAK BOLEH membocorkan field sensitif ke store forensik append-only —
// predikat bersama ini penjaganya; test memastikan field sensitif tertutup sementara field
// domain tetap utuh (konteks forensik tidak hilang sia-sia).
public sealed class AuditRedactionTests
{
    [Theory]
    [InlineData("Password")]
    [InlineData("apiToken")]
    [InlineData("ClientSecret")]
    [InlineData("CardNumber")]
    public void Sensitive_field_names_are_flagged(string propertyName)
        => Assert.True(AuditRedaction.IsSensitive(propertyName));

    [Theory]
    [InlineData("GoodsReceiptId")]
    [InlineData("WarehouseId")]
    [InlineData("Quantity")]
    public void Domain_field_names_are_not_flagged(string propertyName)
        => Assert.False(AuditRedaction.IsSensitive(propertyName));

    [Fact]
    public void Redact_hides_sensitive_values_but_keeps_domain_context()
    {
        var payload = AuditRedaction.Redact(new { GoodsReceiptId = "GR-1", Password = "hunter2" });

        Assert.Contains("GR-1", payload);            // konteks domain dipertahankan
        Assert.Contains("[REDACTED]", payload);      // nilai sensitif disensor
        Assert.DoesNotContain("hunter2", payload);   // rahasia tak pernah mendarat di audit
    }
}
