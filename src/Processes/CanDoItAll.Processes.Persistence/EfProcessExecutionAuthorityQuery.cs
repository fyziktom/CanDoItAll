using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessExecutionAuthorityQuery(
    IDbContextFactory<ProcessPersistenceDbContext> factory,
    TimeProvider clock) : IProcessDecisionDispatchAuthorityQuery {
    public async Task<ProcessExecutionProjectAuthorityResult> ReadAsync(ProcessExecutionClaimEvidence evidence,
        CancellationToken cancellationToken = default) {
        var result = await ReadDispatchAsync(evidence, cancellationToken).ConfigureAwait(false);
        if (result.Snapshot is not { } snapshot) {
            return new(result.Disposition);
        }
        if (snapshot.SourceAuthority is not { } source) {
            return new(ProcessExecutionAuthorityDisposition.AdmissionReconciliationRequired);
        }
        if (source.ProjectAdmission is null || snapshot.ProjectReference is not { } reference) {
            return new(ProcessExecutionAuthorityDisposition.NoProjectScope);
        }
        return new(ProcessExecutionAuthorityDisposition.Bound, new(evidence.ExecutionRunId, evidence.ExecutorAgentId,
            evidence.DispatchClaimToken, reference, source, snapshot.AllowedOperations, snapshot.OperationTargetScope,
            snapshot.CapabilityScope, snapshot.ObservedCurrentDispatch, snapshot.ObservedAtUtc) { DispatchAuthority = snapshot });
    }

    public async Task<ProcessExecutionDispatchAuthorityResult> ReadDispatchAsync(ProcessExecutionClaimEvidence evidence,
        CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ReadCoreAsync(context, evidence, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProcessExecutionDispatchAuthorityResult> ReadDecisionDispatchAsync(ProcessExecutionClaimEvidence evidence,
        ProcessDecisionChildMaterialBinding original, ProcessDecisionExecutionContract supportedExecution,
        ProcessDecisionContentContract? supportedContent, CancellationToken cancellationToken = default) {
        var selection = new ProcessDecisionDispatchSelection(original, supportedExecution, supportedContent, clock);
        selection.Validate(evidence);
        return await WithSelectedReadContextAsync(context =>
            ReadCoreAsync(context, evidence, clock.GetUtcNow(), cancellationToken, selection), cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProcessExecutionReservationObservation> ReadReservationAsync(ProcessExecutionClaimEvidence evidence,
        CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var observed = await ReadCoreAsync(context, evidence, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (observed.Snapshot is not { ObservedCurrentDispatch: true } dispatch) {
            throw Mismatch("The Process source cannot reserve a new execution without its current claim.");
        }
        var payloads = await context.StrategyResultReceipts.AsNoTracking()
            .Where(item => item.RunId == evidence.RunId.Value && item.StepInstanceId == evidence.StepInstanceId.Value)
            .Select(item => item.DiagnosticsJson).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var acknowledged = payloads.Select(ProcessPersistenceMappers.ReadReceiptExecutionRunId)
            .Where(item => item.HasValue).Select(item => item!.Value.Value).ToHashSet();
        var confirmed = await ReadCoreAsync(context, evidence, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (confirmed.Snapshot is not { ObservedCurrentDispatch: true } current || current.OwnerFingerprint != dispatch.OwnerFingerprint) {
            throw Mismatch("The Process source claim changed while its accepted execution outcomes were read.");
        }
        return new(dispatch, acknowledged);
    }

    public async Task<ProcessExecutionDispatchAuthorityResult> ReadOriginalDecisionAsync(ProcessExecutionClaimEvidence evidence,
        ProcessDecisionChildMaterialBinding original, ProcessDecisionExecutionContract supportedExecution,
        ProcessDecisionContentContract? supportedContent, CancellationToken cancellationToken = default) {
        RequireEvidence(evidence);
        var selection = new ProcessDecisionDispatchSelection(original, supportedExecution, supportedContent, clock);
        selection.Validate(evidence);
        return await WithSelectedReadContextAsync(async context => {
            var row = await (from state in context.RuntimeStates.AsNoTracking()
                join claim in context.DispatchClaims.AsNoTracking() on state.RunId equals claim.RunId
                where state.RunId == evidence.RunId.Value && claim.StepInstanceId == evidence.StepInstanceId.Value &&
                    claim.ClaimToken == evidence.DispatchClaimToken
                select new { State = state, Claim = claim }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (row is null || evidence.ExecutionCreatedAtUtc < row.Claim.CreatedAtUtc || evidence.ExecutionCreatedAtUtc >= row.Claim.ExpiresAtUtc) {
                return new(ProcessExecutionAuthorityDisposition.DispatchBindingChanged);
            }
            if (row.State.LaunchAdmissionId is null) {
                throw Mismatch("Original selected receipt evidence requires an accepted Process launch admission.");
            }
            var saved = await EfProcessAcceptedSource.ReadAsync(context, row.State, evidence.StepInstanceId, cancellationToken).ConfigureAwait(false);
            var assignment = saved.Assignment;
            if (assignment.StepKey != evidence.StepKey || assignment.PlanId.Value != row.State.PlanId) {
                throw Mismatch("The original selected assignment differs from its exact Process runtime.");
            }
            await EfProcessDecisionDispatchBinding.RequireOriginalAsync(context, evidence, new(row.State.RootRunId),
                assignment, selection, cancellationToken).ConfigureAwait(false);
            assignment = assignment with { CreatedAtUtc = StoredTimestamp(assignment.CreatedAtUtc) };
            return Snapshot(row.State, evidence, assignment, assignment, saved.Header, false, clock.GetUtcNow(), selection);
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessExecutionDispatchAuthorityResult> WithSelectedReadContextAsync(
        Func<ProcessPersistenceDbContext, Task<ProcessExecutionDispatchAuthorityResult>> read, CancellationToken cancellationToken) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var relational = context.Database.IsRelational();
        try {
            if (relational) {
                await context.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            return await read(context).ConfigureAwait(false);
        } finally {
            if (relational) {
                await context.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
        }
    }

    internal static async Task<ProcessExecutionDispatchAuthorityResult> ReadCoreAsync(ProcessPersistenceDbContext context,
        ProcessExecutionClaimEvidence evidence, DateTimeOffset observedAt, CancellationToken cancellationToken,
        ProcessDecisionDispatchSelection? selection = null) {
        RequireEvidence(evidence);

        var row = await (from state in context.RuntimeStates.AsNoTracking()
                         join savedAssignment in context.RuntimeStepAssignments.AsNoTracking() on state.RunId equals savedAssignment.RunId
                         join step in context.RuntimeSteps.AsNoTracking()
                             on new { savedAssignment.RunId, savedAssignment.StepInstanceId } equals new { step.RunId, step.StepInstanceId }
                         join claim in context.DispatchClaims.AsNoTracking()
                             on new { step.RunId, step.StepInstanceId } equals new { claim.RunId, claim.StepInstanceId }
                         where state.RunId == evidence.RunId.Value && step.StepInstanceId == evidence.StepInstanceId.Value &&
                             claim.ClaimToken == evidence.DispatchClaimToken
                         select new { State = state, Assignment = savedAssignment, Step = step, Claim = claim })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null) {
            return new(ProcessExecutionAuthorityDisposition.DispatchBindingChanged);
        }
        if (row.Assignment.StepKey != evidence.StepKey || row.Assignment.PlanId != row.State.PlanId ||
                !string.Equals(row.Assignment.ExecutorKind, ProcessLaunchExecutorKinds.Agent, StringComparison.OrdinalIgnoreCase) ||
                !Guid.TryParse(row.Assignment.ExecutorId, out var executorId) || executorId != evidence.ExecutorAgentId ||
                evidence.ExecutionCreatedAtUtc < row.Claim.CreatedAtUtc || evidence.ExecutionCreatedAtUtc >= row.Claim.ExpiresAtUtc) {
            return new(ProcessExecutionAuthorityDisposition.DispatchBindingChanged);
        }
        var assignment = EfProcessRuntimeStepAssignmentStore.ToAssignment(row.Assignment);
        if (string.IsNullOrWhiteSpace(assignment.ReadinessHash)) {
            throw Mismatch("The saved Process assignment has no sealed readiness identity.");
        }
        ProcessAcceptedSourceHeader? saved = null;
        var original = ProcessAssignmentSource.From(assignment);
        if (row.State.LaunchAdmissionId is not null) {
            var source = await EfProcessAcceptedSource.ReadAsync(context, row.State, evidence.StepInstanceId, cancellationToken).ConfigureAwait(false);
            saved = source.Header;
            original = source.Assignment;
            if (original.StepKey != assignment.StepKey || original.PlanId != assignment.PlanId ||
                    !original.AllowedOperations.Order(StringComparer.OrdinalIgnoreCase)
                        .SequenceEqual(assignment.AllowedOperations.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase) ||
                    original.OperationTargetScope != assignment.OperationTargetScope) {
                throw Mismatch("The saved Process assignment changed its admitted operation contract.");
            }
        }
        var current = original.DecisionDataPolicy is null && assignment.DecisionDataPolicy is null &&
            evidence.ExecutionMayDispatch && row.State.Status == ProcessRuntimeStatus.Active &&
            row.Step.Status is (ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running) &&
            row.Step.ActiveClaimToken == evidence.DispatchClaimToken && row.Step.AttemptNumber == row.Claim.AttemptNumber &&
            row.Claim.Status is (DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed) && row.Claim.ExpiresAtUtc > observedAt;
        if (selection is not null) {
            (current, observedAt) = await EfProcessDecisionDispatchBinding.RequireAsync(context, evidence, new(row.State.RootRunId),
                original, assignment, selection, cancellationToken).ConfigureAwait(false);
        }
        return Snapshot(row.State, evidence, ProcessAssignmentSource.From(assignment), original, saved, current, observedAt, selection);
    }

    private static ProcessExecutionDispatchAuthorityResult Snapshot(ProcessRuntimeStateEntity state, ProcessExecutionClaimEvidence evidence,
        ProcessAssignmentSource assignment, ProcessAssignmentSource original, ProcessAcceptedSourceHeader? saved,
        bool current, DateTimeOffset observedAt, ProcessDecisionDispatchSelection? selection) {
        var source = saved?.Authority;
        if (source is not null && source.ProjectAdmission != ProcessPersistenceMappers.ReadProjectAdmission(state)) {
            throw Mismatch("The original Process source authority does not match the saved project lifetime.");
        }
        var projectId = ProcessRuntimeLaunchVariables.TryReadProjectId(original.LaunchVariables, out var storedProject) ? storedProject : (Guid?)null;
        if (source?.ProjectAdmission is { } project) {
            if (projectId.HasValue && projectId.Value != project.ProjectId) {
                throw Mismatch("The saved Process assignment targets a different project from its launch admission.");
            }
            projectId = project.ProjectId;
        }
        var fingerprint = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            state.RunId, state.RootRunId, state.PlanId, state.PlanHash,
            evidence.StepInstanceId, evidence.DispatchClaimToken, evidence.ExecutorAgentId,
            assignment.StepKey, assignment.ReadinessHash, assignment.CreatedAtUtc,
            ProjectId = projectId,
            LaunchVariables = original.LaunchVariables.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray(),
            AllowedOperations = assignment.AllowedOperations.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            assignment.OperationTargetScope, original.CapabilityScope,
            PreparationFingerprint = saved?.PreparationFingerprint
        })))).ToLowerInvariant();
        if (selection is not null) {
            fingerprint = selection.Fingerprint(fingerprint, original.DecisionDataPolicy!);
        }
        var reference = saved is null || source?.ProjectAdmission is null ? null : new ProcessExecutionProjectAuthority(
            saved.AdmissionId, saved.PreparationFingerprint, evidence.RunId, evidence.StepInstanceId, assignment.ReadinessHash);
        return new(ProcessExecutionAuthorityDisposition.Bound, new(evidence, new(state.RootRunId), projectId, fingerprint,
            assignment.ReadinessHash, assignment.AllowedOperations, assignment.OperationTargetScope,
            original.CapabilityScope, source, reference, current, observedAt)) {
            OriginalDecisionPolicy = selection is null ? null : original.DecisionDataPolicy!.Policy,
            OriginalChildOnBasisChange = selection is null ? null : original.DecisionDataPolicy!.ChildOnBasisChange
        };
    }

    private static void RequireEvidence(ProcessExecutionClaimEvidence evidence) {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.ExecutionRunId == Guid.Empty || evidence.ExecutorAgentId == Guid.Empty ||
            evidence.RunId.Value == Guid.Empty || evidence.StepInstanceId.Value == Guid.Empty ||
            evidence.DispatchClaimToken == Guid.Empty || string.IsNullOrWhiteSpace(evidence.StepKey)) {
            throw Mismatch("The saved Process execution has incomplete dispatch evidence.");
        }
    }

    internal static DateTimeOffset StoredTimestamp(DateTimeOffset value) {
        if (value == DateTimeOffset.MinValue || value == DateTimeOffset.MaxValue) {
            return value;
        }
        var epoch = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var microseconds = (value.UtcTicks - epoch.Ticks) / TimeSpan.TicksPerMicrosecond;
        return epoch.AddTicks(microseconds * TimeSpan.TicksPerMicrosecond);
    }

    private static ProcessExecutionAuthorityMismatchException Mismatch(string message) => new(message);
}
