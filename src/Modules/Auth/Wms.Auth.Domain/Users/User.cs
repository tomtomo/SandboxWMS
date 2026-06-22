using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Auth.Domain;

// What: Aggregate Root (DDD) — User, identitas yang bisa login (overview §E)
// Why: authority identitas + kredensial. PasswordHash OPAQUE (ADR-0016) — domain menyimpan & mengganti
// string `{algo}.{iter}.{salt}.{hash}` tanpa tahu algoritmanya (KDF di adapter IPasswordHasher). Status
// (Active/Locked/Disabled) gate jalur mint token: HANYA Active lolos (ADR-0012). Lockout (failedLoginCount
// > threshold) = pertahanan brute-force. Merefer Role BY-ID (RoleIds) & Warehouse BY-ID (Vernon IDDD);
// warehouse-scoping di-MODEL tapi enforcement DEFERRED (ADR-0012 → Phase 07a). IAuditable via base.
// How: transisi status guard return Result (no-throw FF#7); koleksi role/warehouse = set idempotent
// di balik List privat. ChangePasswordHash = rehash-on-upgrade (transparan saat login sukses, ADR-0016).
public sealed class User : AuditableAggregateRoot<UserId>
{
    // What: backing store PRIMITIF (Guid) — strongly-typed RoleId di-project di accessor.
    // Why: EF Core mendiskualifikasi koleksi strongly-typed-id (List<RoleId>) sebagai navigation ke
    // entity → simpan Guid (primitive collection bersih), proyeksikan ke RoleId di boundary domain.
    private readonly List<Guid> _roleIds = new();
    private readonly List<Guid> _assignedWarehouseIds = new();

    public string Username { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    // What: kredensial ter-hash OPAQUE (ADR-0016) — domain tak menginterpretasi formatnya
    public string PasswordHash { get; private set; } = null!;

    public UserStatus Status { get; private set; }

    // What: counter lockout policy (overview §E) — direset saat login sukses / unlock
    public int FailedLoginCount { get; private set; }

    // What: Role yang di-assign (RBAC) — sumber permission claim saat mint JWT (role aktif saja, ADR-0012)
    public IReadOnlyCollection<RoleId> RoleIds => _roleIds.Select(id => new RoleId(id)).ToList();

    // What: warehouse yang boleh diakses (scoping) — di-embed claim; enforcement DEFERRED (ADR-0012)
    public IReadOnlyCollection<Guid> AssignedWarehouseIds => _assignedWarehouseIds.AsReadOnly();

    // What: status-derived gate login/mint (ADR-0012) — hanya Active boleh terbit token
    public bool CanAuthenticate => Status == UserStatus.Active;

    private User() { }

    private User(UserId id, string username, string email, string passwordHash) : base(id)
    {
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        Status = UserStatus.Active;
        FailedLoginCount = 0;
    }

    // What: factory — user baru (Active); invariant username/email/passwordHash wajib
    public static Result<User> Create(string username, string email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(username))
            return Result.Failure<User>(UserErrors.MissingUsername);
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<User>(UserErrors.MissingEmail);
        if (string.IsNullOrWhiteSpace(passwordHash))
            return Result.Failure<User>(UserErrors.MissingPasswordHash);

        return Result.Success(new User(UserId.New(), username, email, passwordHash));
    }

    // What: catat percobaan login gagal — increment counter; auto-Lock saat threshold tercapai.
    // Why: pertahanan brute-force (overview §E). Threshold = policy (di-pass dari config oleh handler),
    // bukan magic-constant di domain. Hanya Active yang bisa ter-lock (Disabled/Locked tak relevan).
    public void RecordFailedLogin(int lockThreshold)
    {
        FailedLoginCount++;
        if (Status == UserStatus.Active && FailedLoginCount >= lockThreshold)
            Status = UserStatus.Locked;
    }

    // What: catat login sukses — reset counter lockout; guard hanya saat Active
    public Result RecordSuccessfulLogin()
    {
        if (Status != UserStatus.Active)
            return Result.Failure(UserErrors.NotActive);

        FailedLoginCount = 0;
        return Result.Success();
    }

    // What: rehash-on-upgrade (ADR-0016) — ganti hash transparan saat parameter KDF di-upgrade
    public void ChangePasswordHash(string newPasswordHash) => PasswordHash = newPasswordHash;

    // What: admin force-lock — dari Active ke Locked (login ditolak sementara)
    public void Lock()
    {
        if (Status == UserStatus.Active)
            Status = UserStatus.Locked;
    }

    // What: unlock — Locked → Active + reset counter; guard hanya dari Locked
    public Result Unlock()
    {
        if (Status != UserStatus.Locked)
            return Result.Failure(UserErrors.NotLocked);

        Status = UserStatus.Active;
        FailedLoginCount = 0;
        return Result.Success();
    }

    // What: soft-delete (overview §E: isActive=false → Disabled) — guard cegah double-disable
    public Result Disable()
    {
        if (Status == UserStatus.Disabled)
            return Result.Failure(UserErrors.AlreadyDisabled);

        Status = UserStatus.Disabled;
        return Result.Success();
    }

    // What: re-enable soft-deleted user — Disabled → Active + reset counter.
    // Why: Enable mengontrol HANYA sumbu Disabled (soft-delete); pada user non-Disabled = no-op sukses
    // (lockout adalah sumbu terpisah, dikelola Unlock) — idempoten & tak mencampur dua concern.
    public Result Enable()
    {
        if (Status == UserStatus.Disabled)
        {
            Status = UserStatus.Active;
            FailedLoginCount = 0;
        }

        return Result.Success();
    }

    // What: assign Role (RBAC) — IDEMPOTEN (set semantics); simpan Guid, boundary tetap RoleId
    public void AssignRole(RoleId roleId)
    {
        if (!_roleIds.Contains(roleId.Value))
            _roleIds.Add(roleId.Value);
    }

    public void RemoveRole(RoleId roleId) => _roleIds.Remove(roleId.Value);

    // What: assign warehouse scope — IDEMPOTEN; enforcement DEFERRED (ADR-0012)
    public void AssignWarehouse(Guid warehouseId)
    {
        if (!_assignedWarehouseIds.Contains(warehouseId))
            _assignedWarehouseIds.Add(warehouseId);
    }

    public void RemoveWarehouse(Guid warehouseId) => _assignedWarehouseIds.Remove(warehouseId);
}
