namespace CanDoItAll.AgentFramework.Core;

public sealed record ProjectStructureProcessNodeContextDescriptor(
    string CurrentProcessRunNodeId,
    string ProcessRunNodeId,
    string ParentProcessRunNodeId,
    string TargetProcessRunNodeId)
{
    public bool HasAnyProcessRunNode =>
        !string.IsNullOrWhiteSpace(CurrentProcessRunNodeId) ||
        !string.IsNullOrWhiteSpace(ProcessRunNodeId) ||
        !string.IsNullOrWhiteSpace(ParentProcessRunNodeId) ||
        !string.IsNullOrWhiteSpace(TargetProcessRunNodeId);

    public string PreferredWritebackNodeId =>
        FirstNonEmpty(TargetProcessRunNodeId, ParentProcessRunNodeId, ProcessRunNodeId, CurrentProcessRunNodeId);

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }
}
