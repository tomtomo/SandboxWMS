namespace Wms.BuildingBlocks.Application.Security;

// What: nama claim kustom JWT WMS (ADR-0016 self-contained token; ADR-0012 authZ catalog)
// Why: satu sumbu konstanta supaya nama claim tak tersebar magic-string antara PRODUSEN (Auth issuer
// RS256) dan KONSUMEN (offline validation helper + kelak AuthorizationBehavior Phase 07a). Identitas
// `sub`/username pakai registered claim standar (JwtRegisteredClaimNames) → di-map ke NameIdentifier/Name
// oleh handler, dibaca CurrentUserResolver (ADR-0027). Role/Permission/Warehouse = claim kustom di sini.
public static class WmsClaims
{
    // permission code (`Module.Action`) — sumber enforcement saat authZ di-wire (Phase 07a, ADR-0012)
    public const string Permission = "permission";

    // role code (mis. "ADMIN") — informasional + dasar permission
    public const string Role = "role";

    // warehouse id yang boleh diakses — operational filter, enforcement DEFERRED (ADR-0012)
    public const string Warehouse = "warehouse";
}
