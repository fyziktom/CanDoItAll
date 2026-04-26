namespace CanDoItAll.SharedKernel;

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
