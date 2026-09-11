using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessExecutionAuthorityQuery(
    IDbContextFactory<ProcessPersistenceDbContext> factory,
    TimeProvider clock) {
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

    internal static async Task<ProcessExecutionDispatchAuthorityResult> ReadCoreAsync(ProcessPersistenceDbContext context,
        ProcessExecutionClaimEvidence evidence, DateTimeOffset observedAt, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.ExecutionRunId == Guid.Empty || evidence.ExecutorAgentId == Guid.Empty ||
                evidence.RunId.Value == Guid.Empty || evidence.StepInstanceId.Value == Guid.Empty ||
                evidence.DispatchClaimToken == Guid.Empty || string.IsNullOrWhiteSpace(evidence.StepKey)) {
            throw Mismatch("The saved Process execution has incomplete dispatch evidence.");
        }

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
        ProcessPreparedLaunchSnapshot? saved = null;
        var original = assignment;
        if (row.State.LaunchAdmissionId is { } admissionId) {
            var stored = await context.PreparedLaunches.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == admissionId, cancellationToken).ConfigureAwait(false)
                ?? throw Mismatch("The Process runtime points to a missing saved launch admission.");
            saved = ProcessPreparedLaunchCodec.Read(stored);
            var initial = saved.Preparation.InitialCommit;
            if (stored.RunId != row.State.RunId || stored.PlanId != row.State.PlanId || saved.AcceptedAtUtc is null ||
                    initial.Mutation.State.LaunchAdmissionId?.Value != admissionId ||
                    initial.Mutation.State.PlanHash != row.State.PlanHash ||
                    initial.Mutation.State.ProjectAdmission != ProcessPersistenceMappers.ReadProjectAdmission(row.State)) {
                throw Mismatch("The Process runtime does not match its exact accepted launch admission.");
            }
            original = initial.InitialAssignments?.SingleOrDefault(item => item.StepInstanceId == evidence.StepInstanceId)
                ?? throw Mismatch("The saved Process admission does not include the executing assignment.");
            if (original.StepKey != assignment.StepKey || original.PlanId != assignment.PlanId ||
                    !original.AllowedOperations.Order(StringComparer.OrdinalIgnoreCase)
                        .SequenceEqual(assignment.AllowedOperations.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase) ||
                    original.OperationTargetScope != assignment.OperationTargetScope) {
                throw Mismatch("The saved Process assignment changed its admitted operation contract.");
            }
        }
        var source = saved?.Preparation.Authority;
        if (source is not null && source.ProjectAdmission != ProcessPersistenceMappers.ReadProjectAdmission(row.State)) {
            throw Mismatch("The original Process source authority does not match the saved project lifetime.");
        }
        var projectId = ProcessRuntimeLaunchVariables.TryReadProjectId(original.LaunchVariables, out var storedProject) ? storedProject : (Guid?)null;
        if (source?.ProjectAdmission is { } project) {
            if (projectId.HasValue && projectId.Value != project.ProjectId) {
                throw Mismatch("The saved Process assignment targets a different project from its launch admission.");
            }
            projectId = project.ProjectId;
        }
        var current = evidence.ExecutionMayDispatch && row.State.Status == ProcessRuntimeStatus.Active &&
            row.Step.Status is (ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running) &&
            row.Step.ActiveClaimToken == evidence.DispatchClaimToken && row.Step.AttemptNumber == row.Claim.AttemptNumber &&
            row.Claim.Status is (DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed) && row.Claim.ExpiresAtUtc > observedAt;
        var fingerprint = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            row.State.RunId, row.State.RootRunId, row.State.PlanId, row.State.PlanHash,
            evidence.StepInstanceId, evidence.DispatchClaimToken, evidence.ExecutorAgentId,
            assignment.StepKey, assignment.ReadinessHash, assignment.CreatedAtUtc,
            ProjectId = projectId,
            LaunchVariables = original.LaunchVariables.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray(),
            AllowedOperations = assignment.AllowedOperations.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            assignment.OperationTargetScope, original.CapabilityScope,
            PreparationFingerprint = saved?.PreparationFingerprint
        })))).ToLowerInvariant();
        var reference = saved is null || source?.ProjectAdmission is null ? null : new ProcessExecutionProjectAuthority(
            saved.Preparation.AdmissionId, saved.PreparationFingerprint, evidence.RunId, evidence.StepInstanceId, assignment.ReadinessHash);
        return new(ProcessExecutionAuthorityDisposition.Bound, new(evidence, new(row.State.RootRunId), projectId, fingerprint,
            assignment.ReadinessHash, assignment.AllowedOperations, assignment.OperationTargetScope,
            original.CapabilityScope, source, reference, current, observedAt));
    }

    private static ProcessExecutionAuthorityMismatchException Mismatch(string message) => new(message);
}
