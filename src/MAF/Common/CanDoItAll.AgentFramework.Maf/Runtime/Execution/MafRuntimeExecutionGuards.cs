namespace CanDoItAll.AgentFramework.Maf;

internal sealed class RequiredFinalizerCapturedException(string toolName) : Exception(
    $"Required finalizer tool '{toolName}' was captured.")
{
    public string ToolName { get; } = toolName;
}
