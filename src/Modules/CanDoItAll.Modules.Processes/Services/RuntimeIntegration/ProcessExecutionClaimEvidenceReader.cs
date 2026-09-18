using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessExecutionClaimEvidenceReader {
    internal static ProcessExecutionClaimEvidence? Read(ExecutionRunRecord run, out ProcessExecutionAuthorityDisposition disposition) {
        if (!string.Equals(run.SourceKind, ProcessMockAgentCatalog.ProcessSourceKind, StringComparison.Ordinal) ||
                run.RequestedBy != "process-runtime" || run.RequestedByKind != "system" || run.ChatSessionId is not null) {
            disposition = ProcessExecutionAuthorityDisposition.UnsupportedSource;
            return null;
        }
        if (run.Id == Guid.Empty || !Guid.TryParse(run.ProcessRunId, out var runId) || runId == Guid.Empty ||
                !Guid.TryParse(run.ProcessStepId, out var stepId) || stepId == Guid.Empty ||
                !Guid.TryParse(run.CorrelationId, out var correlationId) || correlationId != runId ||
                !Guid.TryParse(run.CausationId, out var causationId) || causationId != stepId) {
            throw new ProcessExecutionAuthorityMismatchException("The saved Agent execution has inconsistent Process lineage.");
        }
        if (!ProcessDispatchClaimExecutionMetadata.TryRead(run, out var claim)) {
            disposition = ProcessExecutionAuthorityDisposition.AdmissionReconciliationRequired;
            return null;
        }
        disposition = ProcessExecutionAuthorityDisposition.Bound;
        return new(run.Id, run.AgentId, new(runId), new(stepId), claim.Value, run.SourceId, run.CreatedAtUtc,
            run.State is (ExecutionState.Preparing or ExecutionState.Running or ExecutionState.WaitingOnTool) && run.Outcome is null ||
            run.State == ExecutionState.Failed && run.Outcome == RunOutcome.Failed &&
                run.ToolAdmission is { Support: AgentToolAdmissionSupport.Recoverable, BackgroundInput: not null });
    }
}
