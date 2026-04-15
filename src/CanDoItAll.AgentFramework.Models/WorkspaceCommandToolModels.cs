namespace CanDoItAll.AgentFramework.Models;

public sealed record ExecutionBoundaryDescriptor(
    string Mode,
    string FilesystemScope,
    string NetworkScope,
    string CredentialScope,
    string HostLabel,
    bool IsEnforcedByHost,
    string Notes)
{
    public static ExecutionBoundaryDescriptor Unknown { get; } = new(
        Mode: "Unknown",
        FilesystemScope: "Not reported.",
        NetworkScope: "Not reported.",
        CredentialScope: "Not reported.",
        HostLabel: "Unconfigured workspace process host",
        IsEnforcedByHost: false,
        Notes: "No workspace process host has been registered for this runtime.");
}

public sealed record ToolExecutionDecision(
    string ToolName,
    string RecipeId,
    string RiskClass,
    bool Allowed,
    bool ApprovalRequired,
    bool NetworkAllowed,
    bool ExternalRootsAllowed,
    string Reason);

public sealed record WorkspaceCommandExecutionResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string ToolName,
    string RecipeId,
    string RiskClass,
    bool ApprovalRequired,
    ExecutionBoundaryDescriptor Boundary,
    string WorkingDirectory,
    string ArgumentsSummary,
    int ExitCode,
    string StdoutPreview,
    string StderrPreview,
    bool StdoutTruncated,
    bool StderrTruncated);

public sealed record WorkspaceLocalMcpLaunchDescriptor(
    string CapabilityName,
    string Command,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory,
    IReadOnlyDictionary<string, string?> EnvironmentVariables,
    bool ApprovalRequired,
    string RiskClass,
    ExecutionBoundaryDescriptor Boundary,
    WorkspaceToolReceipt Receipt,
    string Message);
