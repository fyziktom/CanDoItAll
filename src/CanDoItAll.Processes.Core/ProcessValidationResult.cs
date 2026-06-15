namespace CanDoItAll.Processes.Core;

public sealed record ProcessValidationFailure(string Code, string Message);

public sealed record ProcessValidationResult(IReadOnlyList<ProcessValidationFailure> Failures)
{
    public bool IsValid => Failures.Count == 0;

    public static ProcessValidationResult Success { get; } = new(Array.Empty<ProcessValidationFailure>());

    public static ProcessValidationResult From(IEnumerable<ProcessValidationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        var failureArray = failures as ProcessValidationFailure[] ?? failures.ToArray();
        return failureArray.Length == 0 ? Success : new ProcessValidationResult(failureArray);
    }
}
