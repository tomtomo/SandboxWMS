using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Wms.BuildingBlocks.Application.Security;

namespace Wms.Platform.Local.Security;

// What: Adapter Local untuk port IPasswordHasher — Argon2id KDF (ADR-0016)
// Why: hashing kredensial pakai KDF LAMBAT memory-hard Argon2id (OWASP Password Storage — winner PHC,
// tahan GPU/ASIC) di balik FORMAT OPAQUE `argon2id.{iter}.{salt}.{hash}` (ADR-0016) → domain tak tahu
// algoritma; ganti KDF = ganti adapter, nol schema change. Constant-time compare (FixedTimeEquals) tahan
// timing-attack; Sentinel precomputed untuk dummy-verify (anti user-enumeration). Pure-managed (Konscious)
// → portable tri-cloud, FF#1 aman. Cloud (Azure/GCP) bisa swap ke adapter berbeda tanpa sentuh core.
// How: salt 16-byte RNG per hash; param OWASP (memory 19 MiB, iter 2, parallelism 1). Verify mem-parse
// format → recompute → FixedTimeEquals → rehash bila iter tersimpan < current (upgrade transparan).
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const string Algo = "argon2id";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    // What: parameter OWASP minimum (memory 19456 KiB ≈ 19 MiB, iterations 2, parallelism 1)
    private const int MemoryKib = 19456;
    private const int CurrentIterations = 2;
    private const int Parallelism = 1;

    // What: hash sentinel precomputed (random input) untuk dummy-verify timing-safe (ADR-0016)
    public string Sentinel { get; }

    public Argon2idPasswordHasher()
    {
        // sekali saat konstruksi (singleton) — input acak ⇒ tak pernah cocok dengan password apa pun
        Sentinel = Hash(Guid.NewGuid().ToString("N"));
    }

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Compute(password, salt, CurrentIterations, HashBytes);
        return string.Join('.',
            Algo, CurrentIterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public PasswordVerificationResult Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('.');
        if (parts.Length != 4 || parts[0] != Algo || !int.TryParse(parts[1], out var iterations))
            return PasswordVerificationResult.Failed;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }

        var actual = Compute(password, salt, iterations, expected.Length);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            return PasswordVerificationResult.Failed;

        // rehash-on-upgrade (ADR-0016): parameter iterasi tersimpan usang → minta stempel ulang
        return iterations == CurrentIterations
            ? PasswordVerificationResult.Success
            : PasswordVerificationResult.SuccessRehashNeeded;
    }

    private static byte[] Compute(string password, byte[] salt, int iterations, int outputBytes)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = Parallelism,
            Iterations = iterations,
            MemorySize = MemoryKib,
        };
        return argon2.GetBytes(outputBytes);
    }
}
