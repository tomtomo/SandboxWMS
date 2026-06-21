namespace Wms.BuildingBlocks.Domain.Results;

// What: error taxonomy untuk Result pattern (ADR-0019)
// Why: satu sumbu ErrorType (5 nilai) yang dipetakan deterministik ke transport
// (ProblemDetails / gRPC status); menambah nilai = keputusan sadar, bukan diam-diam.
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Unexpected
}

// What: Error sebagai value (ADR-0019)
// Why: kegagalan bisnis adalah DATA eksplisit, bukan exception yang bisa ke-swallow;
// Code stabil untuk mapping, Message untuk manusia.
// How: readonly record struct + factory per ErrorType; Error.None menandai sukses.
public readonly record struct Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Unexpected);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);
}
