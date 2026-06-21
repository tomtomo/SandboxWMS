using System.Text.Json;

namespace Wms.BuildingBlocks.Application.Auditing;

// What: predikat PII-redaction bersama untuk payload audit (ADR-0022)
// Why: audit menyimpan KONTEKS command, tapi command bisa membawa field sensitif (password,
// token, credential) yang TAK BOLEH mendarat di store forensik append-only. Satu predikat
// bersama (bukan ad-hoc per command) menjaga kebijakan redaksi konsisten — sumbu tunggal yang
// kalau berubah, berubah di satu tempat. Baseline: match nama properti; klasifikasi PII-per-field
// yang lebih dalam (atribut/skema) di-defer (deep observability Phase 07).
// How: refleksi properti publik command → ganti nilai field yang IsSensitive(name) dengan
// sentinel "[REDACTED]" → serialize JSON. Predikat IsSensitive dipublik supaya bisa diuji unit.
public static class AuditRedaction
{
    private static readonly string[] SensitiveTokens =
        ["password", "secret", "token", "credential", "ssn", "creditcard", "cardnumber", "cvv"];

    // What: predikat PII — true bila nama field tak boleh tampil apa-adanya di audit payload
    public static bool IsSensitive(string propertyName) =>
        SensitiveTokens.Any(token => propertyName.Contains(token, StringComparison.OrdinalIgnoreCase));

    public static string Redact(object command)
    {
        var properties = command.GetType().GetProperties()
            .Where(property => property is { CanRead: true } && property.GetIndexParameters().Length == 0);

        var redacted = new Dictionary<string, object?>();
        foreach (var property in properties)
            redacted[property.Name] = IsSensitive(property.Name)
                ? "[REDACTED]"
                : property.GetValue(command);

        return JsonSerializer.Serialize(redacted);
    }
}
