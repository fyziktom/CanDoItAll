using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService {
    private async Task<ExecutionRunRecord> AdmitBackgroundRunAsync(ExecutionRunRecord run, ExecutionRunRequest request,
        IReadOnlyList<AgentRuntimeInputAttachment> inputAttachments, CancellationToken cancellationToken) {
        if (toolAdmissionJournal is null || !toolAdmissionJournal.SupportsBackgroundSource(run.SourceKind)) {
            return run;
        }
        if (CreateRuntimeContextIntent(run, activityWorkspaceIdentity.WorkspaceScope).Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation) {
            throw new AgentToolAdmissionException("tool-admission.background-purpose-mismatch",
                "The registered background source does not match this execution's trusted purpose.");
        }
        var recoverable = inputAttachments.Count == 0 && (request.TransientContext?.Attachments.IsEmpty ?? true);
        var journal = await toolAdmissionJournal.CreateForNewBackgroundRunAsync(run, request.Prompt.Trim(),
            recoverable ? AgentToolAdmissionSupport.Recoverable : AgentToolAdmissionSupport.RequestScopedInput,
            recoverable ? request.TransientContext : null, cancellationToken);
        return run with { ToolAdmission = journal };
    }
}
