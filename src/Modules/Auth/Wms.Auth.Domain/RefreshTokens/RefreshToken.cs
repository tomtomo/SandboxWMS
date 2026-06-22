using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Auth.Domain;

// What: Aggregate Root (DDD) — RefreshToken, sesi rotasi re-issue access JWT (overview §E, ADR-0016)
// Why: refresh token statis long-lived = target berisiko; aggregate ini mewujudkan ROTATION CHAIN —
// tiap refresh menerbitkan token baru & menandai yang lama `ReplacedByTokenId` + revoke. HASH-ONLY:
// hanya TokenHash (SHA-256 dari 32-byte random) yang dipersist — token mentah tak pernah disimpan
// (batasi dampak DB-compromise). Aggregate root TERSENDIRI (bukan child User) karena di-query by hash
// tiap refresh. Merefer User BY-ID (Vernon IDDD), bukan navigation property. BUKAN auditable: punya
// lifecycle temporal sendiri (issued/expires/revoked) yang lebih ekspresif dari createdBy/at generik.
// How: status DIHITUNG (IsActive = revokedAt==null && now<expiresAt) bukan enum — satu sumbu kebenaran,
// tak bisa drift. Rotate guard hanya-aktif; Revoke idempotent (mendukung cascade reuse-detection ADR-0016).
public sealed class RefreshToken : AggregateRoot<RefreshTokenId>
{
    // What: referensi User by-id (Vernon IDDD) — bukan navigation property lintas-aggregate
    public UserId UserId { get; private set; } = null!;

    // What: HANYA hash yang dipersist (ADR-0016) — token mentah tak pernah masuk DB
    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    // What: timestamp pencabutan (null = belum dicabut) — komponen status terhitung
    public DateTimeOffset? RevokedAt { get; private set; }

    // What: rantai rotasi (ADR-0016) — menunjuk token pengganti; null = belum dirotasi.
    // Why: saat token tercabut disajikan ulang, walk rantai ini → cascade revoke (replay defense).
    public RefreshTokenId? ReplacedByTokenId { get; private set; }

    private RefreshToken() { }

    private RefreshToken(
        RefreshTokenId id, UserId userId, string tokenHash,
        DateTimeOffset issuedAt, DateTimeOffset expiresAt) : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    // What: factory — terbitkan refresh token baru (aktif); invariant tokenHash wajib & expiry>issued
    public static Result<RefreshToken> Issue(
        RefreshTokenId id, UserId userId, string tokenHash,
        DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            return Result.Failure<RefreshToken>(RefreshTokenErrors.MissingTokenHash);
        if (expiresAt <= issuedAt)
            return Result.Failure<RefreshToken>(RefreshTokenErrors.InvalidExpiry);

        return Result.Success(new RefreshToken(id, userId, tokenHash, issuedAt, expiresAt));
    }

    // What: status terhitung (ADR-0016) — aktif HANYA bila belum dicabut DAN belum kedaluwarsa
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    // What: pencabutan IDEMPOTEN — set revokedAt sekali; panggilan ulang tak menggeser timestamp.
    // Why: cascade reuse-detection (ADR-0016) me-revoke seluruh rantai; idempotency menjaga timestamp
    // pencabutan pertama sebagai fakta forensik & membuat cascade aman dipanggil berulang.
    public void Revoke(DateTimeOffset now)
    {
        RevokedAt ??= now;
    }

    // What: rotasi (ADR-0016) — tandai token ini digantikan + revoke, hanya bila masih AKTIF.
    // Why: refresh normal menukar token aktif dengan yang baru; menolak rotasi token tak-aktif menutup
    // celah memutar token kedaluwarsa/tercabut. Reuse token yang sudah tercabut ditangani di sisi handler
    // (deteksi → cascade revoke), bukan di sini — aggregate hanya menegakkan transisi aktif→dirotasi.
    public Result Rotate(RefreshTokenId replacementId, DateTimeOffset now)
    {
        if (!IsActive(now))
            return Result.Failure(RefreshTokenErrors.NotActive);

        ReplacedByTokenId = replacementId;
        RevokedAt = now;
        return Result.Success();
    }
}
