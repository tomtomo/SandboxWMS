namespace Wms.BuildingBlocks.Application.Security;

// What: identitas principal non-manusia (ADR-0027)
// Why: konstanta tunggal supaya "siapa SYSTEM" tak tersebar sebagai magic-string di interceptor,
// behavior, dan test — satu sumbu yang sama dipakai semua jalur origin-mesin. Anonymous dipisah
// EKSPLISIT dari SYSTEM: itulah invariant keamanan ADR-0027 (anonymous HTTP ≠ SYSTEM).
public static class SystemActor
{
    // origin mesin (consumer bus / background job / seeder / s2s) — tak ada HttpContext
    public const string Id = "SYSTEM";

    // request HTTP tanpa identitas terotentikasi — TAK BOLEH dipetakan ke SYSTEM
    public const string Anonymous = "anonymous";
}
