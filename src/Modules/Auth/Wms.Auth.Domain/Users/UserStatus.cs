namespace Wms.Auth.Domain;

// What: status lifecycle User (overview §E) — gate jalur login/mint token
// Why: Active (boleh login & mint JWT) · Locked (failedLoginCount>threshold; auto/admin unlock) ·
// Disabled (admin soft-delete; login ditolak permanen). HANYA Active yang lolos CanAuthenticate
// (ADR-0012 — status non-Active tak boleh terbit token).
// How: disimpan STRING (HasConversion<string>) → urutan enum tak mengikat persistence; aman diurut ulang.
public enum UserStatus
{
    Active,
    Locked,
    Disabled
}
