namespace CanDoItAll.Processes.Application;

public sealed class ProcessProjectionAccessException(string message) : InvalidOperationException(message);
