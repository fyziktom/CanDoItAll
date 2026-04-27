using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class NullAgentExecutionGovernanceBridge : IAgentExecutionGovernanceBridge
{
    public Task OnApprovalsRequestedAsync(
        ExecutionRunRecord run,
        IReadOnlyList<ExecutionApprovalRecord> approvals,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task OnApprovalsDecidedAsync(
        ExecutionRunRecord run,
        IReadOnlyList<ExecutionApprovalRecord> approvals,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public sealed class DurableAgentExecutionGovernanceBridge(IAgentExecutionCheckpointBridge checkpointBridge) : IAgentExecutionGovernanceBridge
{
    private readonly IAgentExecutionCheckpointBridge checkpointBridge = checkpointBridge;

    public async Task OnApprovalsRequestedAsync(
        ExecutionRunRecord run,
        IReadOnlyList<ExecutionApprovalRecord> approvals,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(approvals);

        if (approvals.Count == 0)
        {
            return;
        }

        AgentFrameworkTelemetry.RecordPendingApprovalDelta(run, approvals.Count);
        foreach (var approval in approvals)
        {
            using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("tool.approval.wait", System.Diagnostics.ActivityKind.Internal);
            AgentFrameworkTelemetry.ApplyRunTags(activity, run);
            activity?.SetTag("agentframework.approval_id", approval.ApprovalId);
            activity?.SetTag("agentframework.tool_name", approval.ToolName);
            activity?.SetTag("agentframework.tool_kind", approval.ToolKind);
            activity?.SetTag("agentframework.approval_status", approval.Status.ToString());
        }

        await checkpointBridge.CapturePendingApprovalCheckpointAsync(run, cancellationToken);
    }

    public async Task OnApprovalsDecidedAsync(
        ExecutionRunRecord run,
        IReadOnlyList<ExecutionApprovalRecord> approvals,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(approvals);

        if (approvals.Count == 0)
        {
            return;
        }

        AgentFrameworkTelemetry.RecordPendingApprovalDelta(run, -approvals.Count);
        foreach (var approval in approvals)
        {
            using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("tool.approval.decision", System.Diagnostics.ActivityKind.Internal);
            AgentFrameworkTelemetry.ApplyRunTags(activity, run);
            activity?.SetTag("agentframework.approval_id", approval.ApprovalId);
            activity?.SetTag("agentframework.tool_name", approval.ToolName);
            activity?.SetTag("agentframework.tool_kind", approval.ToolKind);
            activity?.SetTag("agentframework.approval_status", approval.Status.ToString());
            if (approval.DecidedAtUtc.HasValue)
            {
                AgentFrameworkTelemetry.RecordApprovalWaitDuration(run, approval, approval.DecidedAtUtc.Value - approval.RequestedAtUtc);
            }
        }

        await checkpointBridge.MarkCheckpointResumedAsync(run.Id, DateTimeOffset.UtcNow, cancellationToken);
    }
}
