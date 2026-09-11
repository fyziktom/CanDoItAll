using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessWorkflowDispatchAuthority(IDbContextFactory<ProcessPersistenceDbContext> factory,
    DbContextOptions<ProcessPersistenceDbContext> options, CoordinatedDatabaseTransaction transactions, TimeProvider clock)
    : IProcessWorkflowDispatchAuthorityReader, IProcessWorkflowDispatchMutationGuard {
    public async Task<ProcessWorkflowDispatchAuthority> ReadAsync(ProcessWorkflowDispatchRequest request,
        CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await ReadCoreAsync(context, request, clock.GetUtcNow(), cancellationToken);
    }

    public async Task RequireForMutationAsync(ProcessWorkflowDispatchAuthority expected, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(expected);
        await using var context = await transactions.CreateEnlistedAsync(options,
            static configured => new ProcessPersistenceDbContext(configured), cancellationToken);
        if (!context.Database.IsNpgsql()) {
            throw new NotSupportedException("Mapped Workflow dispatch fencing requires the actual PostgreSQL owner transaction.");
        }
        var key = RootLockKey(expected.RootRunId.Value);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
        var request = expected.Request;
        await context.RuntimeStates.FromSqlInterpolated($"""
            SELECT * FROM process_runtime_states WHERE "RunId" = {request.RunId.Value} FOR SHARE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        await context.RuntimeStepAssignments.FromSqlInterpolated($"""
            SELECT * FROM process_runtime_step_assignments
            WHERE "RunId" = {request.RunId.Value} AND "StepInstanceId" = {request.StepInstanceId.Value} FOR SHARE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        await context.RuntimeSteps.FromSqlInterpolated($"""
            SELECT * FROM process_runtime_steps
            WHERE "RunId" = {request.RunId.Value} AND "StepInstanceId" = {request.StepInstanceId.Value} FOR SHARE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        await context.DispatchClaims.FromSqlInterpolated($"""
            SELECT * FROM process_dispatch_claims
            WHERE "RunId" = {request.RunId.Value} AND "ClaimToken" = {request.Claim.Value} FOR SHARE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var current = await ReadCoreAsync(context, request, clock.GetUtcNow(), cancellationToken);
        if (!current.IsCurrent || current.OwnerFingerprint != expected.OwnerFingerprint || current.RootRunId != expected.RootRunId) {
            throw Mismatch("The mapped Workflow's actual Process claim or original assignment no longer permits admission.");
        }
    }

    public async Task<ProcessWorkflowContinuationAuthority> ReadContinuationAsync(ProcessWorkflowContinuationRequest request,
        CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await ReadContinuationCoreAsync(context, request, clock.GetUtcNow(), cancellationToken);
    }

    public async Task RequireForContinuationAsync(ProcessWorkflowContinuationAuthority expected, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(expected);
        await using var context = await transactions.CreateEnlistedAsync(options,
            static configured => new ProcessPersistenceDbContext(configured), cancellationToken);
        if (!context.Database.IsNpgsql()) {
            throw new NotSupportedException("Mapped Workflow continuation fencing requires the actual PostgreSQL owner transaction.");
        }
        var key = RootLockKey(expected.Dispatch.RootRunId.Value);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
        var request = expected.Request;
        await context.RuntimeStates.FromSqlInterpolated($"""
            SELECT * FROM process_runtime_states
            WHERE "RunId" = {request.RunId.Value} OR "RunId" = {expected.Dispatch.RootRunId.Value} FOR SHARE
            """).AsNoTracking().ToArrayAsync(cancellationToken);
        await context.RuntimeStepAssignments.FromSqlInterpolated($"""
            SELECT * FROM process_runtime_step_assignments
            WHERE "RunId" = {request.RunId.Value} AND "StepInstanceId" = {request.StepInstanceId.Value} FOR SHARE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        await context.RuntimeSteps.FromSqlInterpolated($"""
            SELECT * FROM process_runtime_steps
            WHERE "RunId" = {request.RunId.Value} AND "StepInstanceId" = {request.StepInstanceId.Value} FOR SHARE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        await context.DispatchClaims.FromSqlInterpolated($"""
            SELECT * FROM process_dispatch_claims
            WHERE "RunId" = {request.RunId.Value} AND "StepInstanceId" = {request.StepInstanceId.Value} FOR SHARE
            """).AsNoTracking().ToArrayAsync(cancellationToken);
        var current = await ReadContinuationCoreAsync(context, request, clock.GetUtcNow(), cancellationToken);
        if (current.OriginalClaimSequence != expected.OriginalClaimSequence || current.Dispatch.RootRunId != expected.Dispatch.RootRunId ||
                current.Dispatch.OwnerFingerprint != expected.Dispatch.OwnerFingerprint ||
                current.Dispatch.Request != expected.Dispatch.Request || current.ProjectAdmission != expected.ProjectAdmission) {
            throw Mismatch("The original deferred Process assignment changed before its Workflow continuation.");
        }
    }

    private static async Task<ProcessWorkflowContinuationAuthority> ReadContinuationCoreAsync(ProcessPersistenceDbContext context,
        ProcessWorkflowContinuationRequest request, DateTimeOffset now, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RunId.Value == Guid.Empty || request.StepInstanceId.Value == Guid.Empty || request.ChildRunId.Value == Guid.Empty ||
                request.OriginalClaim is { Value: var claimId } && claimId == Guid.Empty ||
                (request.OriginalClaim is null) != (request.OriginalContractHash is null)) {
            throw Mismatch("Mapped Workflow continuation requires its exact retained child and original Process assignment.");
        }
        var state = await context.RuntimeStates.AsNoTracking().Include(item => item.Steps.Where(step => step.StepInstanceId == request.StepInstanceId.Value))
            .SingleOrDefaultAsync(item => item.RunId == request.RunId.Value, cancellationToken)
            ?? throw Mismatch("The original Process runtime is missing.");
        var step = state.Steps.SingleOrDefault() ?? throw Mismatch("The original Process step is missing.");
        var root = await context.RuntimeStates.AsNoTracking().SingleOrDefaultAsync(item => item.RunId == state.RootRunId, cancellationToken);
        if (state.Status != ProcessRuntimeStatus.Active || root?.Status != ProcessRuntimeStatus.Active || !step.IsExecutable ||
                step.Status != ProcessRuntimeStepStatus.Waiting || step.ActiveClaimToken is not null || step.CompletedResultKey is not null) {
            throw Mismatch("Only the original active, uncancelled, deferred Process step can continue its existing Workflow child.");
        }
        var claims = await context.DispatchClaims.AsNoTracking().Where(item => item.RunId == request.RunId.Value &&
            item.StepInstanceId == request.StepInstanceId.Value).ToArrayAsync(cancellationToken);
        var events = await context.RuntimeEvents.AsNoTracking().Where(item => item.RootRunId == state.RootRunId && item.RunId == state.RunId &&
            (item.EventType == ProcessRuntimeEventTypes.DispatchClaimCreated.Value || item.EventType == ProcessRuntimeEventTypes.DispatchClaimReclaimed.Value ||
             item.EventType == ProcessRuntimeEventTypes.DispatchClaimReleased.Value || item.EventType == ProcessRuntimeEventTypes.StepWaiting.Value ||
             item.EventType == ProcessRuntimeEventTypes.StepReady.Value)).OrderBy(item => item.RootSequence).ToArrayAsync(cancellationToken);
        var claimTokens = claims.Select(item => item.ClaimToken.ToString("D")).ToHashSet(StringComparer.Ordinal);
        var creationEvents = events.Where(item => item.EventType == ProcessRuntimeEventTypes.DispatchClaimCreated.Value ||
            item.EventType == ProcessRuntimeEventTypes.DispatchClaimReclaimed.Value).Where(item => claimTokens.Contains(item.PayloadHash)).ToArray();
        var originalToken = request.OriginalClaim?.Value.ToString("D");
        var anchor = originalToken is null ? creationEvents.FirstOrDefault() : creationEvents.SingleOrDefault(item => item.PayloadHash == originalToken);
        if (anchor is null || !Guid.TryParseExact(anchor.PayloadHash, "D", out var anchorToken)) {
            throw Mismatch("The retained Process history cannot identify the original Workflow dispatch; reconciliation is required.");
        }
        var original = claims.Single(item => item.ClaimToken == anchorToken);
        var latest = claims.SingleOrDefault(item => item.AttemptNumber == step.AttemptNumber);
        if (original.Status != DispatchClaimStatus.Released || original.CreatedAtUtc > now || latest is not { Status: DispatchClaimStatus.Released } ||
                latest.CreatedAtUtc > now ||
                !HasDeferredClaim(events, original.ClaimToken, request.ChildRunId.Value, anchor.RootSequence) ||
                !HasDeferredClaim(events, latest.ClaimToken, request.ChildRunId.Value, anchor.RootSequence) ||
                events.Any(item => item.RootSequence > anchor.RootSequence && item.EventType == ProcessRuntimeEventTypes.StepReady.Value &&
                    item.PayloadHash == request.StepInstanceId.ToString())) {
            throw Mismatch("The retained Workflow parent was reworked, replaced, or has no exact released deferral evidence.");
        }
        var retainedRange = context.RuntimeEvents.AsNoTracking().Where(item => item.RootRunId == state.RootRunId && item.RootSequence >= anchor.RootSequence);
        var lastSequence = await retainedRange.MaxAsync(item => item.RootSequence, cancellationToken);
        if (await retainedRange.LongCountAsync(cancellationToken) != lastSequence - anchor.RootSequence + 1) {
            throw Mismatch("The original Process event range is incomplete; Workflow continuation requires reconciliation.");
        }
        var planRow = await context.InstancePlans.AsNoTracking().SingleOrDefaultAsync(item => item.PlanId == state.PlanId, cancellationToken)
            ?? throw Mismatch("The original compiled Process plan is missing.");
        var plan = ProcessInstancePlanPersistenceMapper.Read(planRow).RequireExecutablePlan();
        var snapshot = ProcessPersistenceMappers.ToSnapshot(state);
        var contract = ProcessRuntimeArtifactContracts.BuildStepContract(snapshot, snapshot.Steps.Single(), plan.Branches);
        if (request.OriginalContractHash is { } expectedHash && expectedHash != contract.ContractHash) {
            throw Mismatch("The original Workflow step contract changed before continuation.");
        }
        var dispatch = await ReadCoreAsync(context, new(request.RunId, request.StepInstanceId, new(anchorToken), contract.ContractHash), now, cancellationToken);
        return new(request, dispatch, anchor.RootSequence, ProcessPersistenceMappers.ReadProjectAdmission(state));
    }

    private static bool HasDeferredClaim(IReadOnlyList<ProcessRuntimeEventEntity> events, Guid claimToken, Guid childRunId, long anchorSequence) {
        var token = claimToken.ToString("D");
        var child = childRunId.ToString("D");
        return events.Any(released => released.RootSequence > anchorSequence && released.EventType == ProcessRuntimeEventTypes.DispatchClaimReleased.Value &&
            released.PayloadHash == token && events.Any(waiting => waiting.RootSequence == released.RootSequence - 1 &&
                waiting.EventType == ProcessRuntimeEventTypes.StepWaiting.Value && waiting.CorrelationId == released.CorrelationId &&
                (waiting.PayloadHash == token || waiting.PayloadHash == child)));
    }

    private static async Task<ProcessWorkflowDispatchAuthority> ReadCoreAsync(ProcessPersistenceDbContext context,
        ProcessWorkflowDispatchRequest request, DateTimeOffset now, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RunId.Value == Guid.Empty || request.StepInstanceId.Value == Guid.Empty || request.Claim.Value == Guid.Empty ||
                string.IsNullOrWhiteSpace(request.ContractHash)) {
            throw Mismatch("Mapped Workflow admission requires the actual Process dispatch request.");
        }
        var state = await context.RuntimeStates.AsNoTracking().Include(item => item.Steps.Where(step => step.StepInstanceId == request.StepInstanceId.Value))
            .SingleOrDefaultAsync(item => item.RunId == request.RunId.Value, cancellationToken)
            ?? throw Mismatch("The original Process runtime is missing.");
        var step = state.Steps.SingleOrDefault() ?? throw Mismatch("The original Process step is missing.");
        var row = await context.RuntimeStepAssignments.AsNoTracking().SingleOrDefaultAsync(item =>
            item.RunId == request.RunId.Value && item.StepInstanceId == request.StepInstanceId.Value, cancellationToken)
            ?? throw Mismatch("The mapped Process assignment is missing.");
        var claim = await context.DispatchClaims.AsNoTracking().SingleOrDefaultAsync(item =>
            item.RunId == request.RunId.Value && item.ClaimToken == request.Claim.Value, cancellationToken)
            ?? throw Mismatch("The actual Process dispatch claim is missing.");
        var assignment = EfProcessRuntimeStepAssignmentStore.ToAssignment(row);
        if (row.PlanId != state.PlanId || claim.StepInstanceId != step.StepInstanceId ||
                !string.Equals(assignment.ExecutorKind, ProcessLaunchExecutorKinds.Workflow, StringComparison.OrdinalIgnoreCase) ||
                assignment.WorkflowBinding is not { OutputMapping: ProcessWorkflowOutputMappingKind.ProcessStepOutcome } ||
                string.IsNullOrWhiteSpace(assignment.ReadinessHash)) {
            throw Mismatch("The current Process assignment is not the admitted mapped Workflow executor.");
        }
        var planRow = await context.InstancePlans.AsNoTracking().SingleOrDefaultAsync(item => item.PlanId == state.PlanId, cancellationToken)
            ?? throw Mismatch("The original compiled Process plan is missing.");
        var plan = ProcessInstancePlanPersistenceMapper.Read(planRow).RequireExecutablePlan();
        if (plan.PlanHash != state.PlanHash) {
            throw Mismatch("The original Process plan no longer matches the executing state.");
        }
        var snapshot = ProcessPersistenceMappers.ToSnapshot(state);
        var contract = ProcessRuntimeArtifactContracts.BuildStepContract(snapshot, snapshot.Steps.Single(), plan.Branches);
        if (contract.ContractHash != request.ContractHash || assignment.ProducedArtifactSlotIds.Count != 0 || assignment.RequiredArtifactSlotIds.Count != 0 ||
                contract.RequiredArtifacts.Count != 0 || contract.ExpectedProducedArtifacts.Count != 0 || contract.RequiredRuntimeToolNames.Count != 0 ||
                contract.ArtifactDescriptors.Count != 0 || contract.SubprocessArtifactMappings.Count != 0) {
            throw Mismatch("The mapped Workflow no longer matches its exact supported outcome-only contract.");
        }
        ProcessPreparedLaunchSnapshot? saved = null;
        if (state.LaunchAdmissionId is { } admissionId) {
            var prepared = await context.PreparedLaunches.AsNoTracking().SingleOrDefaultAsync(item => item.Id == admissionId, cancellationToken)
                ?? throw Mismatch("The Process runtime lost its accepted launch admission.");
            saved = ProcessPreparedLaunchCodec.Read(prepared);
            var initial = saved.Preparation.InitialCommit;
            var original = initial.InitialAssignments?.SingleOrDefault(item => item.StepInstanceId == request.StepInstanceId);
            if (saved.AcceptedAtUtc is null || prepared.RunId != state.RunId || prepared.PlanId != state.PlanId ||
                    initial.Mutation.State.LaunchAdmissionId?.Value != admissionId || initial.Mutation.State.PlanHash != state.PlanHash ||
                    initial.Mutation.State.ProjectAdmission != ProcessPersistenceMappers.ReadProjectAdmission(state) || original is null ||
                    original.WorkflowBinding != assignment.WorkflowBinding || original.StepKey != assignment.StepKey || original.PlanId != assignment.PlanId ||
                    original.OperationTargetScope != assignment.OperationTargetScope ||
                    !original.AllowedOperations.Order(StringComparer.OrdinalIgnoreCase).SequenceEqual(
                        assignment.AllowedOperations.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase)) {
                throw Mismatch("The mapped Workflow differs from its original accepted Process admission.");
            }
        }
        var source = saved?.Preparation.Authority;
        if (source is not null && source.ProjectAdmission != ProcessPersistenceMappers.ReadProjectAdmission(state)) {
            throw Mismatch("The original Process source project lifetime changed.");
        }
        var fingerprint = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            Domain = "mapped-process-workflow-v1", state.RunId, state.RootRunId, state.PlanId, state.PlanHash,
            row.StepInstanceId, row.StepKey, row.RoleKey, row.ExecutorKind, row.ExecutorId, row.WorkflowId, row.WorkflowVersionId,
            row.WorkflowOutputMapping, row.Prompt, row.ReadinessHash, row.CreatedAtUtc, request.ContractHash,
            LaunchVariables = assignment.LaunchVariables.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray(),
            PreparationFingerprint = saved?.PreparationFingerprint
        })))).ToLowerInvariant();
        var current = state.Status == ProcessRuntimeStatus.Active && step.IsExecutable &&
            step.Status is (ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running) && step.ActiveClaimToken == request.Claim.Value &&
            step.AttemptNumber == claim.AttemptNumber && claim.Status is (DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed) &&
            claim.CreatedAtUtc <= now && claim.ExpiresAtUtc > now;
        return new(request, new(state.RootRunId), assignment, fingerprint, source, current, now);
    }

    private static long RootLockKey(Guid rootRunId) {
        Span<byte> bytes = stackalloc byte[16];
        rootRunId.TryWriteBytes(bytes);
        return BinaryPrimitives.ReadInt64LittleEndian(bytes[..8]) ^ BinaryPrimitives.ReadInt64LittleEndian(bytes[8..]);
    }

    private static ProcessExecutionAuthorityMismatchException Mismatch(string message) => new(message);
}
