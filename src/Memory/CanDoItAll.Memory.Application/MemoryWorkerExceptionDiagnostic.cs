namespace CanDoItAll.Memory.Application;

internal static class MemoryWorkerExceptionDiagnostic
{
    public static string Create(string action, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(exception);
        return $"{action.Trim()} failed with exception type '{exception.GetType().Name}'.";
    }
}
