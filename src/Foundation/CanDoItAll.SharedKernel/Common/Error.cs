namespace CanDoItAll.SharedKernel;

/// <summary>
/// Severity of an error, as a JSON integer: 0 Info, 1 Warning (for example a rejected request value), 2 Error (a
/// failed command or operation).
/// </summary>
public enum ErrorSeverity
{
    Info,
    Warning,
    Error
}

public sealed record Error(string Code, string Message, ErrorSeverity Severity = ErrorSeverity.Error)
{
    public static Error Validation(string message, string code = "validation") => new(code, message, ErrorSeverity.Warning);

    public static Error Failure(string message, string code = "failure") => new(code, message);
}
