namespace Wms.BuildingBlocks.Application.Security;

// What: hasil verifikasi password (Result-style enum, ADR-0016) — sinyal rehash-on-upgrade
// Why: verifikasi bukan boolean dua-nilai: password BENAR bisa tetap perlu di-rehash bila hash
// tersimpan memakai parameter KDF lama (iterasi/memory di-upgrade). Caller (Login handler) memakai
// SuccessRehashNeeded untuk menstempel ulang PasswordHash transparan saat login sukses — migrasi
// parameter tanpa memaksa reset password. Failed dipakai SERAGAM untuk password salah & dummy-verify
// (user tak dikenal) → anti user-enumeration.
public enum PasswordVerificationResult
{
    Failed,
    Success,
    SuccessRehashNeeded
}

// What: Port (Hexagonal / Ports & Adapters; ADR-0016) — password hashing KDF lambat
// Why: hashing kredensial = detail volatile (algoritma & parameter berevolusi) yang TAK boleh
// mengunci domain — disembunyikan di balik port core-neutral, adapter konkret di Platform.<Cloud>
// (Local = Argon2id/Konscious). Format hash OPAQUE self-describing `{algo}.{iter}.{salt}.{hash}`
// (string di kolom PasswordHash) → domain tak tahu algoritma; ganti KDF = ganti adapter, nol schema
// change. Constant-time compare + dummy-verify (Sentinel) = pertahanan timing-attack & user-enumeration
// (OWASP Password Storage). → Canon: OWASP ASVS/Password Storage Cheat Sheet; Cockburn (Hexagonal).
// How: Hash menghasilkan format opaque; Verify mem-parse format itu, banding constant-time, lapor
// rehash bila parameter usang; Sentinel = hash valid-format precomputed untuk timing-equalization.
public interface IPasswordHasher
{
    // What: produce opaque self-describing hash `{algo}.{iter}.{salt}.{hash}` dari plaintext
    string Hash(string password);

    // What: verifikasi constant-time + sinyal rehash; Failed bila tak cocok / format invalid
    PasswordVerificationResult Verify(string password, string passwordHash);

    // What: hash sentinel valid-format untuk DUMMY verify saat user tak dikenal (anti-enumeration)
    // Why: Login handler memanggil Verify(password, Sentinel) di jalur "user tak ditemukan" supaya
    // biaya CPU (waktu respons) setara jalur "user ada + password salah" — penyerang tak bisa
    // membedakan eksistensi username dari timing. Tak pernah cocok dengan password apa pun.
    string Sentinel { get; }
}
