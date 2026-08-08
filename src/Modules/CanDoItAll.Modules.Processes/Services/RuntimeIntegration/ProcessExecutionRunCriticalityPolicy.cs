using CanDoItAll.AgentFramework.Core.Execution;

namespace CanDoItAll.Modules.Processes;

/// <summary>
/// Governed process-step machine-criticality, moved verbatim from the deleted
/// <c>AgentFrameworkWorkspaceExecutionService.IsGovernedMachineCriticalRun</c> static helper (SB13). A run is
/// machine-critical when any of the three raw process identity signals is present; this intentionally wide OR
/// gate is a defense-in-depth "treat as critical" signal (unlike the narrower governed-process-step admission
/// check), so it stays a single self-contained clause here rather than merging with other process-identity checks.
/// </summary>
public sealed class ProcessExecutionRunCriticalityPolicy : IAgentExecutionRunCriticalityPolicy
{
    public bool IsMachineCritical(AgentExecutionRunCriticalitySnapshot run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return string.Equals(run.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
               !string.IsNullOrWhiteSpace(run.ProcessRunId) ||
               !string.IsNullOrWhiteSpace(run.ProcessStepId);
    }
}
