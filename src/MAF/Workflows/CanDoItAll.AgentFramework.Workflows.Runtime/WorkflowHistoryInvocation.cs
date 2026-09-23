using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Workflows.Runtime;

internal static class WorkflowHistoryInvocation {
    internal static HistoryInvocationContext Create(Guid invocationId) {
        var runId = WorkflowExecutorExecutionAuditScope.CurrentRunId;
        return HistoryInvocationContext.Create(HistoryWorkload.Workflow) with {
            RequestId = new(invocationId),
            Caller = WorkflowExecutorExecutionAuditScope.CurrentOrigin?.HistoryCaller ?? new(HistoryAuthenticationKind.Unknown),
            ExternalReference = WorkflowExecutorExecutionAuditScope.CurrentOrigin is WorkflowLaunchOrigin.ProjectStructureNode project
                ? new(project.ProjectId.ToString("D"), HistoryExternalReference.LocalProjectType) : null,
            Owner = runId is { } run ? new HistoryOwnerIdentity(HistorySourceKind.Workflow,
                new(run.Value.ToString("N")), new(invocationId.ToString("N"))) : null
        };
    }

    internal static WorkflowUsageObservation Attach(WorkflowUsageObservation observation, HistoryInvocationContext context)
        => observation with {
            Id = new(context.RequestId.Value),
            HistoryEvidence = HistoryCanonicalInvocation.Capture(context)
        };
}
