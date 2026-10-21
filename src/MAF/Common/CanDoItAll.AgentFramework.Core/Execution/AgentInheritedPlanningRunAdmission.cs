using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentInheritedPlanningRunPreparation(ExecutionRunRecord Candidate, string OriginalInput,
    AgentPlanningOwner Owner, AgentToolSemanticDigest RuntimeContract, WorkspaceScopeDescriptor Scope,
    AgentToolProtocolEnvelope ParentBinding,
    Func<AgentInheritedPlanningBindingContext, CancellationToken, Task<AgentInheritedPlanningBinding>> BindAsync);

public sealed record AgentInheritedPlanningRunAdmissionResult(ExecutionRunRecord Run, bool Created);

public sealed class AgentInheritedPlanningRunAdmission(ISandboxWorkspaceExecutionRunStore runs,
    ISandboxWorkspaceExecutionRunLeaseStore leases, AgentToolAdmissionJournal journal) {
    public const string MissingOriginalCode = "planning.inherited-original-missing";

    public Task<AgentInheritedPlanningRunAdmissionResult> AdmitAsync(Guid executionRunId, AgentInheritedPlanningStart start,
        Func<ExecutionRunRecord, CancellationToken, Task> requireOriginalParent,
        Func<CancellationToken, Task<AgentInheritedPlanningRunPreparation>> prepare,
        CancellationToken cancellationToken = default)
        => AdmitAsync(executionRunId, start, requireOriginalParent, prepare, null, cancellationToken);

    internal Task<AgentInheritedPlanningRunAdmissionResult> AdmitWorkspaceAsync(AgentInheritedPlanningWorkspaceRequest request,
        Func<ExecutionRunRecord, CancellationToken, Task> requireOriginalParent,
        Func<CancellationToken, Task<AgentInheritedPlanningRunPreparation>> prepare, CancellationToken cancellationToken)
        => AdmitAsync(request.ExecutionRunId, request.Start, requireOriginalParent, prepare,
            new(request, journal.Profile, journal.Clock), cancellationToken);

    private async Task<AgentInheritedPlanningRunAdmissionResult> AdmitAsync(Guid executionRunId, AgentInheritedPlanningStart start,
        Func<ExecutionRunRecord, CancellationToken, Task> requireOriginalParent,
        Func<CancellationToken, Task<AgentInheritedPlanningRunPreparation>> prepare,
        AgentInheritedPlanningPreparationAttempt? attempt, CancellationToken cancellationToken) {
        if (executionRunId == Guid.Empty) {
            throw new ArgumentException("Inherited planning requires its exact child execution ID.", nameof(executionRunId));
        }
        ArgumentNullException.ThrowIfNull(requireOriginalParent);
        ArgumentNullException.ThrowIfNull(prepare);
        ArgumentNullException.ThrowIfNull(start);
        start.Validate();
        var original = await runs.GetExecutionRunAsync(executionRunId, cancellationToken);
        if (original is not null) {
            await RequireOriginalAsync(original, executionRunId, start, requireOriginalParent, cancellationToken);
            return new(original, false);
        }
        var lease = await leases.AcquireToolDispatchLeaseAsync(executionRunId, cancellationToken);
        AgentInheritedPlanningRunAdmissionResult result;
        try {
            result = await AdmitCoreAsync(executionRunId, start, requireOriginalParent, prepare, attempt, cancellationToken);
        } catch (Exception primary) {
            List<Exception> failures = [primary];
            (DateTimeOffset ClosedAtUtc, long WorkTicks)? closed = null;
            try {
                closed = attempt?.ObserveCompletion();
            } catch (Exception measurement) {
                failures.Add(measurement);
            }
            try {
                await lease.DisposeAsync();
            } catch (Exception cleanup) {
                failures.Add(cleanup);
            }
            if (failures.Count > 1) {
                throw new AggregateException(failures);
            }
            if (closed is { } measured) {
                throw attempt!.CreateFailure(measured, primary);
            }
            throw;
        }
        await lease.DisposeAsync();
        return result;
    }

    private async Task<AgentInheritedPlanningRunAdmissionResult> AdmitCoreAsync(Guid executionRunId, AgentInheritedPlanningStart start,
        Func<ExecutionRunRecord, CancellationToken, Task> requireOriginalParent,
        Func<CancellationToken, Task<AgentInheritedPlanningRunPreparation>> prepare,
        AgentInheritedPlanningPreparationAttempt? attempt, CancellationToken cancellationToken) {
        var existing = await runs.GetExecutionRunAsync(executionRunId, cancellationToken);
        if (existing is not null) {
            await RequireOriginalAsync(existing, executionRunId, start, requireOriginalParent, cancellationToken);
            return new(existing, false);
        }
        if (start.Mode != AgentInheritedPlanningStartMode.FreshEnrollment) {
            throw new AgentToolAdmissionException(MissingOriginalCode,
                "The original inherited child is unavailable; only fresh parent enrollment may prepare it. Reconcile the original allocation.");
        }
        using var deadline = new CancellationTokenSource(start.PreparationLimit, journal.Clock);
        using var preparationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        return await PrepareAsync(executionRunId, start, requireOriginalParent, prepare, attempt, preparationCancellation.Token);
    }

    private async Task<AgentInheritedPlanningRunAdmissionResult> PrepareAsync(Guid executionRunId, AgentInheritedPlanningStart start,
        Func<ExecutionRunRecord, CancellationToken, Task> requireOriginalParent,
        Func<CancellationToken, Task<AgentInheritedPlanningRunPreparation>> prepare,
        AgentInheritedPlanningPreparationAttempt? attempt, CancellationToken cancellationToken) {
        var startedAt = journal.Clock.GetUtcNow();
        var started = journal.Clock.GetTimestamp();
        attempt?.Begin(startedAt, started);
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
            preparation.Owner, preparation.ParentBinding, preparation.BindAsync, cancellationToken);
        candidate = candidate with { ToolAdmission = admission };
        await RequireOriginalAsync(candidate, executionRunId, start, requireOriginalParent, cancellationToken);
        var now = journal.Clock.GetUtcNow();
        var elapsed = journal.Clock.GetElapsedTime(started);
        if (elapsed < TimeSpan.Zero || now < startedAt) {
            throw new AgentPlanningAdmissionException(admission.PlanningDecision!.DecisionId, 1, AgentPlanningAdmissionRefusal.ClockInvalid);
        }
        var decision = AgentPlanningDecisionTransitions.CloseInitialPreparation(admission.PlanningDecision!,
            TimeSpan.FromTicks(Math.Max(elapsed.Ticks, (now - startedAt).Ticks)), now);
        var exhausted = decision.Current.State == AgentPlanningGenerationState.Failed;
        candidate = candidate with {
            ToolAdmission = admission with { PlanningDecision = decision }, UpdatedAtUtc = now,
            State = exhausted ? ExecutionState.Failed : ExecutionState.Preparing,
            Outcome = exhausted ? RunOutcome.Failed : null, CompletedAtUtc = exhausted ? now : null
        };
        candidate.ToolAdmission.Validate();
        attempt?.BeginSave();
        var saved = await runs.SaveExecutionRunDetailAsync(new(candidate, null, [], []), cancellationToken);
        return new(saved.Run, true);
    }

    private async Task RequireOriginalAsync(ExecutionRunRecord run, Guid executionRunId, AgentInheritedPlanningStart start,
        Func<ExecutionRunRecord, CancellationToken, Task> requireOriginalParent, CancellationToken cancellationToken) {
        if (run.Id != executionRunId || run.ToolAdmission?.SchemaVersion != AgentToolJournalRecord.InheritedPlanningSchemaVersion ||
            run.ToolAdmission.PlanningDecision?.InheritedOrigin is null) {
            throw Failure("The child execution is not an original inherited planning run.");
        }
        start.RequireOriginal(run.ToolAdmission.PlanningDecision.InheritedOrigin);
        _ = AgentPlanningRunMetadata.Read(run);
        if (!await journal.CanReadBackgroundResultAsync(run, cancellationToken)) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        await requireOriginalParent(run, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static AgentToolAdmissionException Failure(string message) => new("planning.inherited-admission", message);
}
