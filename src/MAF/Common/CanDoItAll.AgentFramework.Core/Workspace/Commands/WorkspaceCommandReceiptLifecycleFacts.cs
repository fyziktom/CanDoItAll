namespace CanDoItAll.AgentFramework.Core;

public interface IWorkspaceCommandReceiptLifecycleFactExtractor
{
    IReadOnlyList<WorkspaceCommandReceiptLifecycleFact> Extract(
        WorkspaceCommandReceiptLifecycleFactContext context);
}

public sealed record WorkspaceCommandReceiptLifecycleFactContext(
    string ToolName,
    IReadOnlyList<string> Arguments,
    IReadOnlyList<string> TargetPaths,
    string? Stdout,
    string? Stderr);

public sealed record WorkspaceCommandReceiptLifecycleFact(
    string Name,
    string Value)
{
    public string Format()
        => string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Value)
            ? string.Empty
            : $"{Name.Trim()}={Value.Trim()}";
}
