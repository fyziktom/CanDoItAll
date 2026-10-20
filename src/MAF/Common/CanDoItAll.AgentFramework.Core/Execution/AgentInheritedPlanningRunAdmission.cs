using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentInheritedPlanningRunPreparation(ExecutionRunRecord Candidate, string OriginalInput,
    AgentPlanningOwner Owner, AgentToolSemanticDigest RuntimeContract, WorkspaceScopeDescriptor Scope,
    Func<AgentToolSessionAdmission, AgentInheritedPlanningBinding> Bind);

public sealed record AgentInheritedPlanningRunAdmissionResult(ExecutionRunRecord Run, bool Created);

public sealed class AgentInheritedPlanningRunAdmission(ISandboxWorkspaceExecutionRunStore runs,
    ISandboxWorkspaceExecutionRunLeaseStore leases, AgentToolAdmissionJournal journal) {
    public async Task<AgentInheritedPlanningRunAdmissionResult> AdmitAsync(Guid executionRunId,
        Func<ExecutionRunRecord, CancellationToken, Task> requireOriginalParent,
        Func<CancellationToken, Task<AgentInheritedPlanningRunPreparation>> prepare,
        CancellationToken cancellationToken = default) {
        if (executionRunId == Guid.Empty) {
            throw new ArgumentException("Inherited planning requires its exact child execution ID.", nameof(executionRunId));
        }
        ArgumentNullException.ThrowIfNull(requireOriginalParent);
        ArgumentNullException.ThrowIfNull(prepare);
        var original = await runs.GetExecutionRunAsync(executionRunId, cancellationToken);
        if (original is not null) {
            await RequireOriginalAsync(original, executionRunId, requireOriginalParent, cancellationToken);
            return new(original, false);
        }
        var lease = await leases.AcquireToolDispatchLeaseAsync(executionRunId, cancellationToken);
        AgentInheritedPlanningRunAdmissionResult result;
        try {
            result = await AdmitCoreAsync(executionRunId, requireOriginalParent, prepare, cancellationToken);
        } catch (Exception primary) {
            try {
                await lease.DisposeAsync();
            } catch (Exception cleanup) {
                throw new AggregateException(primary, cleanup);
            }
            throw;
        }
        await lease.DisposeAsync();
        return result;
    }

    private async Task<AgentInheritedPlanningRunAdmissionResult> AdmitCoreAsync(Guid executionRunId,
        Func<ExecutionRunRecord, CancellationToken, Task> requireOriginalParent,
        Func<CancellationToken, Task<AgentInheritedPlanningRunPreparation>> prepare, CancellationToken cancellationToken) {
        var existing = await runs.GetExecutionRunAsync(executionRunId, cancellationToken);
        if (existing is not null) {
            await RequireOriginalAsync(existing, executionRunId, requireOriginalParent, cancellationToken);
            return new(existing, false);
        }
        var preparation = await prepare(cancellationToken)
            ?? throw Failure("Inherited planning preparation returned no candidate.");
        var candidate = preparation.Candidate;
        if (candidate is null || candidate.Id != executionRunId || candidate.ToolAdmission is not null ||
            candidate.ChatSessionId is not null || candidate.Revision != 1 || candidate.State != ExecutionState.Preparing ||
            candidate.Outcome is not null || candidate.CompletedAtUtc is not null || candidate.PendingApprovals.Count != 0 ||
            candidate.AutoApprovePendingToolCalls || candidate.SerializedSessionStateJson is not null ||
            candidate.ResultSummary.Length != 0 || candidate.StructuredOutputContractKey.Length != 0 ||
            candidate.StructuredOutputJsonSchema.Length != 0 || candidate.StructuredOutputRawOutput.Length != 0) {
            throw Failure("Inherited planning requires a new, empty candidate for its exact child execution.");
        }
        candidate = candidate with {
            MetadataJson = AgentPlanningRunMetadata.ApplyInherited(candidate.MetadataJson, preparation.Owner,
                preparation.RuntimeContract, preparation.Scope)
        };
        var admission = await new AgentInheritedPlanningAdmission(journal).CreateAsync(candidate, preparation.OriginalInput,
            preparation.Owner, preparation.Bind, cancellationToken);
        candidate = candidate with { ToolAdmission = admission };
        await RequireOriginalAsync(candidate, executionRunId, requireOriginalParent, cancellationToken);
        var saved = await runs.SaveExecutionRunDetailAsync(new(candidate, null, [], []), cancellationToken);
        return new(saved.Run, true);
    }

    private async Task RequireOriginalAsync(ExecutionRunRecord run, Guid executionRunId,
        Func<ExecutionRunRecord, CancellationToken, Task> requireOriginalParent, CancellationToken cancellationToken) {
        if (run.Id != executionRunId || run.ToolAdmission?.SchemaVersion != AgentToolJournalRecord.InheritedPlanningSchemaVersion ||
            run.ToolAdmission.PlanningDecision?.InheritedOrigin is null) {
            throw Failure("The child execution is not an original inherited planning run.");
        }
        _ = AgentPlanningRunMetadata.Read(run);
        if (!await journal.CanReadBackgroundResultAsync(run, cancellationToken)) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        await requireOriginalParent(run, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static AgentToolAdmissionException Failure(string message) => new("planning.inherited-admission", message);
}
