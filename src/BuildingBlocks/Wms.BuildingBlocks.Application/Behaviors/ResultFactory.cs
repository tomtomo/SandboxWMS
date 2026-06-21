using System.Reflection;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.BuildingBlocks.Application.Behaviors;

// What: factory generic Result-failure untuk pipeline behavior (Result pattern, ADR-0019)
// Why: ValidationBehavior wajib mengembalikan kegagalan ber-tipe TResponse (Result ATAU
// Result<T>) TANPA throw — padahal TResponse generic. Helper ini menyusun failure yang benar
// per-tipe sehingga no-throw-for-business tetap terjaga di pipeline.
// How: static generic class → field di-init SEKALI per TResponse (JIT-cached, nol refleksi
// per-request); Result<T> dibangun via Result.Failure<T>(error) lewat refleksi, Result
// non-generic via cast langsung.
internal static class ResultFactory<TResponse>
    where TResponse : Result
{
    public static readonly Func<Error, TResponse> Failure = Build();

    private static Func<Error, TResponse> Build()
    {
        if (typeof(TResponse) == typeof(Result))
            return error => (TResponse)Result.Failure(error);

        // TResponse == Result<TValue> → Result.Failure<TValue>(Error) (mengembalikan Result<TValue>)
        var valueType = typeof(TResponse).GetGenericArguments()[0];
        var failureMethod = typeof(Result)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == nameof(Result.Failure)
                              && method.IsGenericMethodDefinition
                              && method.GetParameters() is [{ ParameterType: var parameterType }]
                              && parameterType == typeof(Error))
            .MakeGenericMethod(valueType);

        return error => (TResponse)failureMethod.Invoke(null, [error])!;
    }
}
