using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceExecutionService
{
    private enum ExecutionRunContinuationDisposition
    {
        Started,
        AlreadyFinalized,
        AlreadyInProgress
    }

    private sealed record PreparedExecutionRunContinuation(
        AgentExecutionPreparationBlueprint Blueprint,
        SandboxWorkspaceCatalogSnapshot CatalogSnapshot,
        ExecutionRunRecord OriginalRun,
        ExecutionRunRecord TransitionedRun,
        ChatSessionRecord? Session,
        AgentDefinition Agent,
        ProviderProfile Provider,
        IReadOnlyList<ExecutionApprovalRecord> RunApprovals,
        IReadOnlyList<ExecutionApprovalRecord> DecidedApprovals);

    private sealed record ExecutionRunContinuationStart(
        ExecutionRunContinuationDisposition Disposition,
        PreparedExecutionRunContinuation? Prepared = null);

    private async Task<ExecutionRunRecord> LoadExecutionRunAsync(
        Guid executionRunId,
        CancellationToken cancellationToken)
    {
        return (await LoadExecutionRunDetailAsync(executionRunId, cancellationToken)).Run;
    }

    private async Task<ExecutionRunResult> LoadExistingExecutionRunResultAsync(
        Guid executionRunId,
        CancellationToken cancellationToken)
    {
        var detail = await LoadExecutionRunDetailAsync(executionRunId, cancellationToken);
        var metric = detail.Metrics
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("This execution run does not have a persisted metric result yet.");
        var assistantMessage = detail.ChatSession?.Messages.LastOrDefault(item => item.Role == ChatMessageRole.Assistant);
        var responseText = !string.IsNullOrWhiteSpace(detail.Run.StructuredOutputRawOutput)
            ? detail.Run.StructuredOutputRawOutput
            : assistantMessage?.Content ?? detail.Run.ResultSummary;
        var jsonSchemaOutput = AgentJsonSchemaOutputContractProcessor.Restore(detail.Run);
        var structuredOutput = jsonSchemaOutput is null ||
                               string.IsNullOrEmpty(detail.Run.StructuredOutputValidationStatus)
            ? null
            : AgentJsonSchemaOutputContractProcessor.ValidateOutput(jsonSchemaOutput, responseText);

        return new ExecutionRunResult(
            executionRunId,
            detail.Run.ChatSessionId,
            responseText,
            assistantMessage,
            metric)
        {
            State = detail.Run.State,
            StructuredOutput = structuredOutput
        };
    }

    private async Task<ExecutionRunContinuationStart> BeginPendingApprovalContinuationAsync(
        AgentExecutionPreparationSnapshot preparation,
        ExecutionRunRecord expectedRun,
        ProviderProfile provider,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls,
        CancellationToken cancellationToken)
    {
        var allApproved = decisions.All(decision => decision.Approved);
        if (store is ISandboxWorkspaceExecutionRunMutationStore mutationStore)
        {
            return await BeginPendingApprovalContinuationWithSplitStoreAsync(
                mutationStore,
                preparation,
                expectedRun,
                provider,
                decisions,
                autoApprovePendingToolCalls,
                cancellationToken);
        }

        ExecutionRunContinuationStart? result = null;

        await store.UpdateWorkspaceAsync(document =>
        {
            var catalog = document.ToCatalog();
            var catalogSnapshot = new SandboxWorkspaceCatalogSnapshot(
                catalog,
                catalog.CatalogDataRevision);
            var executionState = document.ToExecutionState();
            var currentRun = executionState.ExecutionRuns.FirstOrDefault(item => item.Id == expectedRun.Id)
                ?? throw new InvalidOperationException("Execution run was not found.");
            var currentSession = currentRun.ChatSessionId.HasValue
                ? executionState.ChatSessions.FirstOrDefault(item => item.Id == currentRun.ChatSessionId.Value)
                    ?? throw new InvalidOperationException("Chat session was not found.")
                : null;

            if (currentRun.PendingApprovals.Count == 0)
            {
                result = new(
                    currentRun.State is ExecutionState.Completed or ExecutionState.Failed
                        ? ExecutionRunContinuationDisposition.AlreadyFinalized
                        : ExecutionRunContinuationDisposition.AlreadyInProgress);
                return document;
            }

            if (currentRun.State != ExecutionState.WaitingOnTool)
            {
                result = new(ExecutionRunContinuationDisposition.AlreadyInProgress);
                return document;
            }

            if (!PendingApprovalStateMatches(expectedRun, currentRun))
            {
                throw new InvalidOperationException(
                    "This execution run's pending approval state changed before the continuation could start. Reload the workspace and try again.");
            }

            EnsurePreparationCurrentForUse(
                preparation.Blueprint,
                catalogSnapshot);
            var agent = catalog.Agents.FirstOrDefault(item => item.Id == currentRun.AgentId)
                ?? throw new InvalidOperationException("Agent was not found.");
            if (!agent.ProviderProfileId.HasValue)
            {
                throw new InvalidOperationException("The selected agent does not have a provider profile.");
            }
            EnsureContinuationProviderLeaseMatches(currentRun, provider);

            var decidedAtUtc = DateTimeOffset.UtcNow;
            var effectiveAutoApprove = allApproved && (autoApprovePendingToolCalls || currentRun.AutoApprovePendingToolCalls);
            var approvalDecision = ExecutionRunStateTransitions.ApplyApprovalDecision(
                executionState.ExecutionApprovals.Where(item => item.ExecutionRunId == currentRun.Id).ToList(),
                currentRun,
                decisions,
                decidedAtUtc,
                currentRun.ChatSessionId.HasValue ? "chat-session" : "execution-run",
                currentRun.ChatSessionId?.ToString("N") ?? currentRun.Id.ToString("N"),
                toolPolicies);
            var transitionedRun = ExecutionRunStateTransitions.CreateContinuationStartRun(
                currentRun,
                allApproved,
                effectiveAutoApprove,
                decidedAtUtc);
            if (currentRun.ToolAdmission is { Support: AgentToolAdmissionSupport.Recoverable, Segments.Length: > 0 }) {
                transitionedRun = transitionedRun with { ToolAdmission = AgentToolJournalTransitions.ApplyDecisions(
                    currentRun.ToolAdmission, currentRun.PendingApprovals, decisions, automatic: false) };
            }
            var transitionedSession = currentSession is null
                ? null
                : ChatSessionRuntimeCompatibilityAdapter.ClearCompatibility(
                    currentSession,
                    transitionedRun.UpdatedAtUtc,
                    currentRun.Id);
            var updatedExecutionState = executionState with
            {
                ExecutionRuns = ReplaceExecutionRun(executionState.ExecutionRuns, transitionedRun),
                ChatSessions = transitionedSession is null
                    ? executionState.ChatSessions
                    : ReplaceChatSession(executionState.ChatSessions, transitionedSession),
                ExecutionApprovals = ReplaceRunApprovals(
                    executionState.ExecutionApprovals,
                    transitionedRun.Id,
                    approvalDecision.RunApprovals)
            };

            result = new(
                ExecutionRunContinuationDisposition.Started,
                new PreparedExecutionRunContinuation(
                    preparation.Blueprint,
                    catalogSnapshot,
                    currentRun,
                    transitionedRun,
                    transitionedSession,
                    agent,
                    provider,
                    approvalDecision.RunApprovals,
                    approvalDecision.Decided));

            return SandboxWorkspaceDocument.Combine(catalog, updatedExecutionState);
        }, cancellationToken);

        return result ?? throw new InvalidOperationException("Execution run continuation could not be prepared.");
    }

    private async Task<ExecutionRunContinuationStart> BeginPendingApprovalContinuationWithSplitStoreAsync(
        ISandboxWorkspaceExecutionRunMutationStore mutationStore,
        AgentExecutionPreparationSnapshot preparation,
        ExecutionRunRecord expectedRun,
        ProviderProfile provider,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls,
        CancellationToken cancellationToken)
    {
        var allApproved = decisions.All(decision => decision.Approved);
        ExecutionRunContinuationStart? result = null;

        await mutationStore.UpdateExecutionRunDetailAsync(
            expectedRun.Id,
            (catalog, currentDetail) =>
            {
                var catalogSnapshot = new SandboxWorkspaceCatalogSnapshot(
                    catalog,
                    catalog.CatalogDataRevision);
                var currentRun = currentDetail.Run;
                var currentSession = currentRun.ChatSessionId.HasValue
                    ? currentDetail.ChatSession
                        ?? throw new InvalidOperationException("Chat session was not found.")
                    : null;

                if (currentRun.PendingApprovals.Count == 0)
                {
                    result = new(
                        currentRun.State is ExecutionState.Completed or ExecutionState.Failed
                            ? ExecutionRunContinuationDisposition.AlreadyFinalized
                            : ExecutionRunContinuationDisposition.AlreadyInProgress);
                    return currentDetail;
                }

                if (currentRun.State != ExecutionState.WaitingOnTool)
                {
                    result = new(ExecutionRunContinuationDisposition.AlreadyInProgress);
                    return currentDetail;
                }

                if (!PendingApprovalStateMatches(expectedRun, currentRun))
                {
                    throw new InvalidOperationException(
                        "This execution run's pending approval state changed before the continuation could start. Reload the workspace and try again.");
                }

                EnsurePreparationCurrentForUse(
                    preparation.Blueprint,
                    catalogSnapshot);
                var agent = catalog.Agents.FirstOrDefault(item => item.Id == currentRun.AgentId)
                    ?? throw new InvalidOperationException("Agent was not found.");
                if (!agent.ProviderProfileId.HasValue)
                {
                    throw new InvalidOperationException("The selected agent does not have a provider profile.");
                }
                EnsureContinuationProviderLeaseMatches(
                    currentRun,
                    provider);

                var decidedAtUtc = DateTimeOffset.UtcNow;
                var effectiveAutoApprove = allApproved && (autoApprovePendingToolCalls || currentRun.AutoApprovePendingToolCalls);
                var approvalDecision = ExecutionRunStateTransitions.ApplyApprovalDecision(
                    currentDetail.Approvals,
                    currentRun,
                    decisions,
                    decidedAtUtc,
                    currentRun.ChatSessionId.HasValue ? "chat-session" : "execution-run",
                    currentRun.ChatSessionId?.ToString("N") ?? currentRun.Id.ToString("N"),
                    toolPolicies);
                var transitionedRun = ExecutionRunStateTransitions.CreateContinuationStartRun(
                    currentRun,
                    allApproved,
                    effectiveAutoApprove,
                    decidedAtUtc);
                if (currentRun.ToolAdmission is { Support: AgentToolAdmissionSupport.Recoverable, Segments.Length: > 0 }) {
                    transitionedRun = transitionedRun with { ToolAdmission = AgentToolJournalTransitions.ApplyDecisions(
                        currentRun.ToolAdmission, currentRun.PendingApprovals, decisions, automatic: false) };
                }
                var transitionedSession = currentSession is null
                    ? null
                    : ChatSessionRuntimeCompatibilityAdapter.ClearCompatibility(
                        currentSession,
                        transitionedRun.UpdatedAtUtc,
                        currentRun.Id);

                result = new(
                    ExecutionRunContinuationDisposition.Started,
                    new PreparedExecutionRunContinuation(
                        preparation.Blueprint,
                        catalogSnapshot,
                        currentRun,
                        transitionedRun,
                        transitionedSession,
                        agent,
                        provider,
                        approvalDecision.RunApprovals,
                        approvalDecision.Decided));

                return currentDetail with
                {
                    Run = transitionedRun,
                    ChatSession = transitionedSession,
                    Approvals = approvalDecision.RunApprovals
                };
            },
            cancellationToken);

        return result ?? throw new InvalidOperationException("Execution run continuation could not be prepared.");
    }

    private async Task<ExecutionRunContinuationStart> BeginAdmittedRunRecoveryAsync(
        AgentExecutionPreparationSnapshot preparation, ExecutionRunRecord expected, ProviderProfile provider,
        AgentToolRunLease lease, CancellationToken cancellationToken) {
        var writer = store as ISandboxWorkspaceExecutionRunMutationStore
            ?? throw new NotSupportedException("Admitted-run recovery requires the canonical current-state execution writer.");
        ExecutionRunContinuationStart? result = null;
        await writer.UpdateExecutionRunDetailAsync(expected.Id, (catalog, detail) => {
            var run = detail.Run;
            var journal = RequireRecoverableToolJournal(run);
            if (run.Revision != expected.Revision || journal.ActiveDispatchLeaseId != lease.Id ||
                journal.Session.Reference != lease.Session) {
                throw new AgentToolAdmissionException("tool-admission.recovery-conflict", "The admitted run changed during recovery preparation.");
            }

            if (run.PendingApprovals.Count != 0 || journal.Segments[^1].ApprovalCheckpoint is not null &&
                journal.Segments[^1].PendingApprovals.Any(approval => AgentToolJournalTransitions.RequireProposal(journal,
                    approval.ToolAdmission ?? throw new InvalidDataException("The saved approval has no intent binding.")).ApprovalStatus == ExecutionApprovalStatus.Pending)) {
                throw new AgentToolAdmissionException("tool-admission.approval-pending", "Decide the exact saved pending approvals before recovering this invocation.");
            }

            if (run.State == ExecutionState.Completed || run.Outcome == RunOutcome.Cancelled) {
                throw new AgentToolAdmissionException("tool-admission.terminal-run", "This execution is terminal and cannot dispatch a recovered effect.");
            }

            var snapshot = new SandboxWorkspaceCatalogSnapshot(catalog, catalog.CatalogDataRevision);
            EnsurePreparationCurrentForUse(preparation.Blueprint, snapshot);
            EnsureContinuationProviderLeaseMatches(run, provider);
            var agent = catalog.Agents.Single(item => item.Id == run.AgentId);
            if (agent.Status != AgentLifecycleStatus.Active || agent.IsTemplate) {
                throw new AgentToolAdmissionException("tool-admission.actor-unavailable", "The admitted agent is no longer active.");
            }

            var session = detail.ChatSession ?? throw new InvalidDataException("The admitted chat was not found.");
            if (session.Id != journal.Session.Reference.ChatSessionId || session.AgentId != run.AgentId || session.LatestExecutionRunId != run.Id) {
                throw new AgentToolAdmissionException("tool-admission.chat-mismatch", "The saved run is no longer this chat's current execution.");
            }

            var transitioned = run with { State = ExecutionState.Running, Outcome = null, CompletedAtUtc = null,
                ResultSummary = "Recovering the original admitted invocation.", UpdatedAtUtc = DateTimeOffset.UtcNow,
                Revision = checked(run.Revision + 1) };
            result = new(ExecutionRunContinuationDisposition.Started, new(preparation.Blueprint, snapshot, run,
                transitioned, session, agent, provider, detail.Approvals, []));
            return detail with { Run = transitioned };
        }, cancellationToken);
        return result ?? throw new InvalidOperationException("The owner did not prepare admitted-run recovery.");
    }

    private static AgentToolJournalRecord RequireRecoverableToolJournal(ExecutionRunRecord run) {
        var journal = run.ToolAdmission ?? throw new AgentToolAdmissionException("tool-admission.legacy-run",
            "This run has no durable tool admission journal; no effect identity can be fabricated for recovery.");
        journal.Validate();
        if (journal.Support != AgentToolAdmissionSupport.Recoverable || journal.OriginalInput is null || journal.Segments.Length == 0) {
            throw new AgentToolAdmissionException("tool-admission.unsupported-recovery",
                "This run has no supported immutable input and SDK checkpoint for recovery.");
        }

        if (journal.Batches.SelectMany(batch => batch.Proposals).Any(proposal =>
                proposal.State is AgentToolProposalState.Executing or AgentToolProposalState.ReconciliationRequired &&
                proposal.Payload.Recovery == AgentToolProposalRecovery.ReconcileBeforeRetry)) {
            throw new AgentToolAdmissionException("tool-admission.reconciliation-required",
                "A previously dispatched non-idempotent effect is uncertain. Reconcile it before recovery or another model proposal.");
        }

        return journal;
    }

    private AgentRuntimeTransientContext? ResolveAdmittedRuntimeContext(ExecutionRunRecord run) {
        if (!ExecutionInvocationMetadata.RequiresTransientContext(run) ||
            run.ToolAdmission is not { Support: AgentToolAdmissionSupport.Recoverable, RuntimeContext: { } saved }) {
            return transientContextRegistry.Resolve(run);
        }

        var context = saved.ToTransientContext();
        if (AgentChatContextDigest.Compute(context) != ExecutionInvocationMetadata.ResolveTransientContextDigest(run)) {
            throw new AgentToolAdmissionException("tool-admission.context-mismatch", "The saved runtime context does not match its original admitted digest.");
        }

        try {
            return transientContextRegistry.Resolve(run);
        } catch (AgentRunTransientContextUnavailableException) {
            transientContextRegistry.Register(run, context);
            return context;
        }
    }

    private async Task ValidateAdmittedResultReadAsync(ExecutionRunRecord run, CancellationToken cancellationToken) {
        var contextScope = ResolveAdmittedRuntimeContext(run)?.WorkspaceScope
            ?? ExecutionInvocationMetadata.ResolveContextWorkspaceScope(run);
        var original = ResolveValidatedExecutionGovernance(run, contextScope, activityWorkspaceIdentity)
            ?? throw new AgentToolAdmissionException("tool-admission.authority-missing", "The completed run has no trusted authority projection.");
        var reference = AgentTurnContextMetadata.TryReadTurnContextReference(run.MetadataJson)
            ?? throw new AgentToolAdmissionException("tool-admission.context-missing", "The completed run has no original source identity.");
        var resolver = executionAuthorityResolver
            ?? throw new InvalidOperationException("Completed result disclosure requires the canonical current authority resolver.");
        AgentExecutionAuthorityRecord current;
        try {
            current = await resolver.ResolveAsync(new(run.AgentId, reference.SourceKind, reference.SourceId,
                original.WorkspaceScope, activityWorkspaceIdentity.DatabaseProfileGeneration, UiAccessHint: null), cancellationToken);
        } catch (AgentExecutionAuthorityMismatchException) {
            throw new AgentToolAdmissionException("tool-admission.result-read-denied", "Current authorization does not permit reading this completed execution result.");
        }

        var currentRead = AgentExecutionGovernanceSnapshot.FromAuthority(current);
        if (!original.ReadAllowed || !current.ReadAllowed || current.AgentId != original.AgentId ||
            current.DatabaseProfileId != original.DatabaseProfileId || current.DatabaseProfileGeneration != original.DatabaseProfileGeneration ||
            current.WorkspaceScope != original.WorkspaceScope ||
            !CoversReadCeiling(currentRead.AllowedCapabilityKeys, original.AllowedCapabilityKeys) ||
            !CoversReadCeiling(currentRead.ReadOnlyExternalTargetAliases.Union(currentRead.WritableExternalTargetAliases),
                original.ReadOnlyExternalTargetAliases.Union(original.WritableExternalTargetAliases))) {
            throw new AgentToolAdmissionException("tool-admission.result-read-denied", "Current authorization does not permit reading this completed execution result.");
        }

        static bool CoversReadCeiling(IReadOnlySet<string> current, IReadOnlySet<string> original)
            => current.Count == 0 || original.Count != 0 && current.IsSupersetOf(original);
    }

    private async Task ValidateAdmittedResumeAuthorityAsync(ExecutionRunRecord run, CancellationToken cancellationToken) {
        if (run.ToolAdmission is not { Support: AgentToolAdmissionSupport.Recoverable, Segments.Length: > 0 }) {
            return;
        }

        var contextScope = ResolveAdmittedRuntimeContext(run)?.WorkspaceScope
            ?? ExecutionInvocationMetadata.ResolveContextWorkspaceScope(run);
        var authority = ResolveValidatedExecutionGovernance(run, contextScope, activityWorkspaceIdentity)
            ?? throw new AgentToolAdmissionException("tool-admission.authority-missing", "The admitted run has no trusted authority projection.");
        var reference = AgentTurnContextMetadata.TryReadTurnContextReference(run.MetadataJson)
            ?? throw new AgentToolAdmissionException("tool-admission.context-missing", "The admitted run has no original source identity.");
        var current = await (executionAuthorityResolver ?? throw new InvalidOperationException("Durable recovery requires the canonical current authority resolver."))
            .ResolveAsync(new(run.AgentId, reference.SourceKind, reference.SourceId,
                authority.WorkspaceScope, activityWorkspaceIdentity.DatabaseProfileGeneration,
                UiAccessHint: null), cancellationToken);
        if (current.AgentId != authority.AgentId || current.DatabaseProfileId != authority.DatabaseProfileId ||
            current.DatabaseProfileGeneration != authority.DatabaseProfileGeneration || current.WorkspaceScope != authority.WorkspaceScope ||
            !current.ReadAllowed || authority.MutationAllowed && !current.MutationAllowed ||
            current.PolicyVersion != authority.PolicyVersion || current.PolicyFingerprint != authority.PolicyFingerprint ||
            !authority.AllowedOperations.SetEquals(current.AllowedOperations) ||
            !authority.AllowedCapabilityKeys.SetEquals(current.AllowedCapabilityKeys) ||
            !authority.WritableExternalTargetAliases.SetEquals(current.AllowedExternalTargetAliases) ||
            !authority.ReadOnlyExternalTargetAliases.SetEquals(current.ReadOnlyExternalTargetAliases)) {
            throw new AgentToolAdmissionException("tool-admission.authority-changed",
                "Current authorization no longer matches the saved execution authority. Recovery cannot widen or replace the original grants.");
        }
    }

    private static bool PendingApprovalStateMatches(
        ExecutionRunRecord expectedRun,
        ExecutionRunRecord currentRun)
        => ExecutionRunStateTransitions.PendingApprovalStateMatches(expectedRun, currentRun);

    /// <summary>
    /// Projects application-owned per-proposal decisions onto the runtime port's decision
    /// shape unmangled — no collapse to a single boolean. Callers must have already validated
    /// (<see cref="AgentApprovalDecisionMismatchException.ValidateExactCoverage"/>) that
    /// <paramref name="decisions"/> exactly covers the run's pending approvals.
    /// </summary>
    private static IReadOnlyList<AgentRuntimeApprovalDecision> ToRuntimeApprovalDecisions(
        IReadOnlyList<PendingToolApprovalDecision> decisions)
    {
        return decisions
            .Select(decision => new AgentRuntimeApprovalDecision(decision.ApprovalId, decision.Approved))
            .ToArray();
    }
}
