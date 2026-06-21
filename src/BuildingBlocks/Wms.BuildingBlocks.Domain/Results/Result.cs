namespace Wms.BuildingBlocks.Domain.Results;

// What: Result Pattern (ADR-0019)
// Why: business failure dikembalikan sebagai NILAI — caller dipaksa handle eksplisit;
// `throw` disisakan untuk programmer-error/infra. Mendasari no-throw-for-business
// + rollback-on-Result.Failure.
// How: IsSuccess + Error; invariant "sukses XOR error" dijaga di ctor.
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Result sukses tidak boleh membawa Error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Result gagal wajib membawa Error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.Success(value);

    public static Result<TValue> Failure<TValue>(Error error) => Result<TValue>.Failure(error);
}

// What: Result<T> — Result yang membawa nilai saat sukses (ADR-0019)
// How: Value hanya valid saat IsSuccess; akses saat gagal = programmer-error → throw.
public sealed class Result<TValue> : Result
{
    private readonly TValue _value;

    private Result(TValue value) : base(true, Error.None) => _value = value;

    private Result(Error error) : base(false, error) => _value = default!;

    public TValue Value => IsSuccess
        ? _value
        : throw new InvalidOperationException("Tidak boleh mengakses Value dari Result yang gagal.");

    public static Result<TValue> Success(TValue value) => new(value);

    public static new Result<TValue> Failure(Error error) => new(error);
}
