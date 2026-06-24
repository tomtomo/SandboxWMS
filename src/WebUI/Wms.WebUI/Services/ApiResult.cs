namespace Wms.WebUI.Services;

// What: envelope hasil command (POST/PUT/DELETE) — terpisah dari query yang return T?.
// Why: command butuh sinyal success + pesan error untuk graceful-degrade UI; query cukup return data/null.
public sealed record ApiResult(bool Success, string? ErrorMessage = null)
{
    public static ApiResult Ok() => new(true, null);
    public static ApiResult Fail(string message) => new(false, message);
}

public sealed record ApiResult<T>(bool Success, T? Value, string? ErrorMessage = null)
{
    public static ApiResult<T> Ok(T value) => new(true, value, null);
    public static ApiResult<T> Fail(string message) => new(false, default, message);
}
