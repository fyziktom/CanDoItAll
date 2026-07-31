using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static CanDoItAll.Modules.Processes.ProcessCompletionIssueResultFactory;

namespace CanDoItAll.Modules.Processes;


internal sealed class AgentFrameworkProcessExecutionClaimRecoveryCoordinator(
    ICanDoItAllAgentWorkspaceFactory workspaceFactory,
    IProcessProjectionClock clock,
    IProcessRuntimeStateStore stateStore,
    IProcessInstancePlanStore planStore,
    IProcessRuntimeStepAssignmentStore assignmentStore,
    IProcessRuntimeUnitOfWork unitOfWork,
    IProcessRuntimeDispatchQueue dispatchQueue,
    ProcessRuntimeBranchSignalApplicationService branchSignalRouter,
    ProcessRuntimeProjectionCatchupService projectionCatchupService,
    ProcessStepCompletionCoordinator completionCoordinator,
    ILogger<AgentFrameworkProcessExecutionClaimRecoveryCoordinator> logger)
{
    private const int MaximumConcurrencyRetries = 3;
    private static readonly TimeSpan ConcurrencyRetryDelay = TimeSpan.FromMilliseconds(100);

    public async Task<bool> BlockRecoveredExecutionClaimAsync(
        ExecutionRunRecord executionRun,
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId,
        string requestedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executionRun);

        if (executionRun.Id == Guid.Empty)
        {
            throw new ArgumentException(
                "A concrete recovered AgentFramework execution run id is required.",
                nameof(executionRun));
        }

        var normalizedRequestedBy = NormalizeRequestedBy(requestedBy);
        for (var attempt = 1; attempt <= MaximumConcurrencyRetries; attempt++)
        {
            try
            {
                return await TryBlockRecoveredExecutionClaimAsync(
                    executionRun,
                    runId,
                    stepInstanceId,
                    normalizedRequestedBy,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt < MaximumConcurrencyRetries)
            {
                await Task.Delay(ConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    public static bool IsRecoverableExecutionFailure(ExecutionState state, RunOutcome? outcome)
        => state == ExecutionState.Failed &&
           outcome is RunOutcome.Cancelled or RunOutcome.Failed;

    public static bool IsRecoverableExecutionCompletion(ExecutionState state, RunOutcome? outcome)
        => state == ExecutionState.Completed &&
           outcome == RunOutcome.Succeeded;

    public async Task<bool> SubmitRecoveredExecutionResultAsync(
        ExecutionRunRecord executionRun,
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId,
        string requestedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executionRun);

        var normalizedRequestedBy = NormalizeRequestedBy(requestedBy);
        for (var attempt = 1; attempt <= MaximumConcurrencyRetries; attempt++)
        {
            try
            {
                return await TrySubmitRecoveredExecutionResultAsync(
                    executionRun,
                    runId,
                    stepInstanceId,
                    normalizedRequestedBy,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt < MaximumConcurrencyRetries)
            {
                await Task.Delay(ConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    private async Task<bool> TryBlockRecoveredExecutionClaimAsync(
        ExecutionRunRecord executionRun,
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId,
        string requestedBy,
        CancellationToken cancellationToken)
    {
        if (!IsExecutionForProcessStep(executionRun, runId, stepInstanceId))
        {
            logger.LogInformation(
                "Skipping interrupted process execution recovery for execution run {ExecutionRunId} because its recorded process run or step identity does not match {RunId}/{StepInstanceId}.",
                executionRun.Id,
                runId.Value,
                stepInstanceId.Value);
            return false;
        }

        if (!IsRecoverableExecutionFailure(executionRun.State, executionRun.Outcome))
        {
            return false;
        }

        var state = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false);
        if (state is null || ProcessRuntimeTerminalStates.IsRunTerminal(state.Status))
        {
            return false;
        }

        var step = state.Steps.FirstOrDefault(candidate => candidate.StepInstanceId == stepInstanceId);
        if (step is null ||
            step.ActiveClaimToken is not { } claimToken ||
            step.Status is not (ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running))
        {
            return false;
        }

        var claim = state.Claims.FirstOrDefault(candidate =>
            candidate.StepInstanceId == stepInstanceId &&
            candidate.ClaimToken == claimToken &&
            candidate.Status is DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed);
        if (claim is null)
        {
            return false;
        }

        if (!CanAssociateClaimWithRecoveredExecution(
                claim.CreatedAtUtc,
                claim.ExpiresAtUtc,
                executionRun.CreatedAtUtc) ||
            !IsExecutionBoundToClaim(executionRun, claim.ClaimToken.Value))
        {
            logger.LogInformation(
                "Skipping interrupted process execution recovery for execution run {ExecutionRunId} because the execution does not belong to active claim {ClaimToken}. ProcessRunId={RunId} StepInstanceId={StepInstanceId} ClaimCreatedAtUtc={ClaimCreatedAtUtc} ExecutionCreatedAtUtc={ExecutionCreatedAtUtc}",
                executionRun.Id,
                claim.ClaimToken,
                runId.Value,
                stepInstanceId.Value,
                claim.CreatedAtUtc,
                executionRun.CreatedAtUtc);
            return false;
        }

        var assignment = await assignmentStore
            .LoadAsync(runId, stepInstanceId, cancellationToken)
            .ConfigureAwait(false);
        if (assignment is null)
        {
            logger.LogWarning(
                "Interrupted AgentFramework execution run {ExecutionRunId} could not be reconciled because process assignment {RunId}/{StepInstanceId} was not found.",
                executionRun.Id,
                runId.Value,
                stepInstanceId.Value);
            return false;
        }

        var adapterResult = CreateInterruptedExecutionReplayUnsafeResult(
            assignment,
            executionRun);
        var result = CreateRecoveredStrategyResult(executionRun, adapterResult);
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var commit = await engine.SubmitStrategyResultAsync(
            state,
            CreateContext(requestedBy),
            new SubmitStrategyResultCommand(
                stepInstanceId,
                claim.OwnerId,
                claim.ClaimToken,
                new StrategyResultIdempotencyKey(result.IdempotencyKey),
                result),
            cancellationToken).ConfigureAwait(false);
        if (!commit.Succeeded)
        {
            logger.LogWarning(
                "Interrupted process execution recovery could not block claim {ClaimToken} for run {RunId}, step {StepInstanceId}. Diagnostics={Diagnostics}",
                claim.ClaimToken,
                runId.Value,
                stepInstanceId.Value,
                string.Join("; ", commit.Diagnostics.Select(diagnostic => diagnostic.Message)));
            return false;
        }

        await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);

        logger.LogWarning(
            "Interrupted process execution recovery blocked claim {ClaimToken} for execution run {ExecutionRunId} because side-effect completion is indeterminate and automatic replay is unsafe. ProcessRunId={RunId} StepInstanceId={StepInstanceId}",
            claim.ClaimToken,
            executionRun.Id,
            runId.Value,
            stepInstanceId.Value);
        return true;
    }

    private static ProcessExecutionAdapterResult CreateInterruptedExecutionReplayUnsafeResult(
        ProcessRuntimeStepAssignment assignment,
        ExecutionRunRecord executionRun)
    {
        var summary =
            $"Agent execution '{executionRun.Id:D}' for step '{assignment.StepKey}' was interrupted before the process runtime received a terminal result. Durable execution identity cannot prove whether external side effects completed, so automatic replay is forbidden and operator review is required.";
        var evidence =
            $"{assignment.RunId}:{assignment.StepInstanceId}:{executionRun.Id:D}:{executionRun.State}:{executionRun.Outcome}:side-effects-indeterminate";
        return NeedsManagerForCompletionIssue(
            assignment,
            ComputeHash(evidence),
            new ProcessCompletionIssue(
                ProcessExecutionAdapterDiagnosticCodes.AgentInterruptedExecutionReplayUnsafe,
                summary,
                evidence,
                assignment.ProducedArtifactSlotIds,
                ProcessDiagnosticRetrySafety.UnsafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Unknown)) with
        {
            ExecutionRunId = new ProcessExecutionRunId(executionRun.Id)
        };
    }

    private async Task<bool> TrySubmitRecoveredExecutionResultAsync(
        ExecutionRunRecord executionRun,
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId,
        string requestedBy,
        CancellationToken cancellationToken)
    {
        if (!IsRecoverableExecutionCompletion(executionRun.State, executionRun.Outcome))
        {
            return false;
        }

        if (!IsExecutionForProcessStep(executionRun, runId, stepInstanceId))
        {
            logger.LogInformation(
                "Skipping recovered AgentFramework execution result {ExecutionRunId} because its recorded process run or step identity does not match {RunId}/{StepInstanceId}.",
                executionRun.Id,
                runId.Value,
                stepInstanceId.Value);
            return false;
        }

        var assignment = await assignmentStore.LoadAsync(runId, stepInstanceId, cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            logger.LogWarning(
                "Completed AgentFramework execution run {ExecutionRunId} could not be reconciled because process assignment {RunId}/{StepInstanceId} was not found.",
                executionRun.Id,
                runId.Value,
                stepInstanceId.Value);
            return false;
        }

        var validation = AgentOutputJson.DeserializeAndValidate(
            executionRun.ResultSummary,
            new ProcessStepOutcomeValidator());
        if (!validation.Succeeded || validation.Output is null)
        {
            logger.LogWarning(
                "Completed AgentFramework execution run {ExecutionRunId} could not be reconciled because its process step output was invalid. RawOutputHash={RawOutputHash} Errors={Errors}",
                executionRun.Id,
                validation.RawOutputHash,
                string.Join("; ", validation.Validation.Errors.Select(error => $"{error.Code}: {error.Message}")));
            return false;
        }

        var state = await stateStore.LoadAsync(runId, cancellationToken).ConfigureAwait(false);
        if (state is null || ProcessRuntimeTerminalStates.IsRunTerminal(state.Status))
        {
            return false;
        }

        var step = state.Steps.FirstOrDefault(candidate => candidate.StepInstanceId == stepInstanceId);
        if (step is null ||
            step.ActiveClaimToken is not { } claimToken ||
            step.Status is not (ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running))
        {
            return false;
        }

        var claim = state.Claims.FirstOrDefault(candidate =>
            candidate.StepInstanceId == stepInstanceId &&
            candidate.ClaimToken == claimToken &&
            candidate.Status is DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed);
        if (claim is null)
        {
            return false;
        }

        if (!CanAssociateClaimWithRecoveredExecution(
                claim.CreatedAtUtc,
                claim.ExpiresAtUtc,
                executionRun.CreatedAtUtc) ||
            !IsExecutionBoundToClaim(executionRun, claim.ClaimToken.Value))
        {
            logger.LogInformation(
                "Skipping recovered AgentFramework execution result {ExecutionRunId} because the execution does not belong to active claim {ClaimToken}. ProcessRunId={RunId} StepInstanceId={StepInstanceId} ClaimCreatedAtUtc={ClaimCreatedAtUtc} ExecutionCreatedAtUtc={ExecutionCreatedAtUtc}",
                executionRun.Id,
                claim.ClaimToken,
                runId.Value,
                stepInstanceId.Value,
                claim.CreatedAtUtc,
                executionRun.CreatedAtUtc);
            return false;
        }

        var context = CreateContext(
            requestedBy,
            NormalizeRecoveredResultTimestamp(executionRun.CompletedAtUtc ?? executionRun.UpdatedAtUtc, claim.ExpiresAtUtc));
        var toolReceipts = await LoadRecoveredExecutionToolReceiptsAsync(
                executionRun,
                cancellationToken)
            .ConfigureAwait(false);
        var plan = await planStore.LoadAsync(state.PlanId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Process run '{runId}' references missing plan '{state.PlanId}'.");
        var stepContract = ProcessRuntimeArtifactContracts.BuildStepContract(
            state,
            step,
            plan.Branches);
        var materialization = completionCoordinator.Materialize(
            assignment,
            validation.Output,
            executionRun.Id,
            toolReceipts,
            stepContract);
        ProcessExecutionAdapterResult adapterResult;
        if (materialization.Issue is { } materializationIssue)
        {
            adapterResult = NeedsManagerForCompletionIssue(
                assignment,
                validation.RawOutputHash,
                materializationIssue);
        }
        else
        {
            var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
            var completionToolReceipts = await completionCoordinator.LoadCompletionToolReceiptsAsync(
                    workspaceService,
                    assignment,
                    executionRun.Id,
                    materialization.ToolReceipts,
                    cancellationToken)
                .ConfigureAwait(false);
            adapterResult = completionCoordinator.Complete(
                assignment,
                materialization,
                validation.RawOutputHash,
                executionRun.Id,
                completionToolReceipts,
                appendRuntimeGateFindings: true,
                stepContract: stepContract);
        }

        var result = CreateRecoveredStrategyResult(executionRun, adapterResult);
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var commit = await engine.SubmitStrategyResultAsync(
            state,
            context,
            new SubmitStrategyResultCommand(
                stepInstanceId,
                claim.OwnerId,
                claim.ClaimToken,
                new StrategyResultIdempotencyKey(result.IdempotencyKey),
                result),
            cancellationToken).ConfigureAwait(false);
        if (!commit.Succeeded)
        {
            logger.LogWarning(
                "Completed AgentFramework execution run {ExecutionRunId} could not submit process result for run {RunId}, step {StepInstanceId}. Diagnostics={Diagnostics}",
                executionRun.Id,
                runId.Value,
                stepInstanceId.Value,
                string.Join("; ", commit.Diagnostics.Select(diagnostic => diagnostic.Message)));
            return false;
        }

        await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);
        await branchSignalRouter.ApplyForResultAsync(
            commit.State,
            plan,
            result,
            requestedBy,
            cancellationToken).ConfigureAwait(false);
        dispatchQueue.EnqueueOrDefer(
            new ProcessRuntimeDispatchQueueRequest(runId, requestedBy));

        logger.LogInformation(
            "Recovered completed AgentFramework execution run {ExecutionRunId} into process run {RunId}, step {StepInstanceId}. Outcome={Outcome}",
            executionRun.Id,
            runId.Value,
            stepInstanceId.Value,
            adapterResult.Outcome);
        return true;
    }

    private async Task<IReadOnlyList<ToolExecutionReceiptRecord>> LoadRecoveredExecutionToolReceiptsAsync(
        ExecutionRunRecord executionRun,
        CancellationToken cancellationToken)
    {
        try
        {
            var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
            var executionDetail = await workspaceService
                .GetExecutionRunDetailAsync(executionRun.Id, cancellationToken)
                .ConfigureAwait(false);
            return executionDetail.ToolReceipts;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "Completed AgentFramework execution run {ExecutionRunId} could not load tool receipts during process recovery. Recovery will fall back to the structured result without receipts.",
                executionRun.Id);
            return [];
        }
    }

    private static StrategyResultEnvelope CreateRecoveredStrategyResult(
        ExecutionRunRecord executionRun,
        ProcessExecutionAdapterResult adapterResult)
    {
        var strategy = StandardProcessAdapterDescriptors.WorkflowAdapter.Strategy;
        var idempotencyKey = CreateDeterministicGuid($"agent-framework-process-result:{executionRun.Id:N}");
        return new StrategyResultEnvelope(
            strategy.StrategyId,
            strategy.StrategyVersion,
            idempotencyKey,
            adapterResult.Outcome,
            adapterResult.ProducedArtifacts,
            adapterResult.RequestedArtifacts,
            adapterResult.Diagnostics
                .Select(diagnostic => new StrategyDiagnosticRef(
                    diagnostic.Code,
                    diagnostic.Sensitivity,
                    diagnostic.EvidenceHash,
                    diagnostic.SafeSummary,
                    diagnostic.RestrictedEvidenceReference,
                    diagnostic.RetrySafety,
                    diagnostic.Idempotency)
                {
                    RelatedChildRunId = diagnostic.RelatedChildRunId,
                    ExecutionSafetyAttestation = diagnostic.ExecutionSafetyAttestation
                })
                .ToArray(),
            adapterResult.ManagerSignals,
            adapterResult.ResultHash)
        {
            UserSafeSummary = adapterResult.UserSafeSummary,
            ExecutionRunId = adapterResult.ExecutionRunId
        };
    }

    private static DateTimeOffset NormalizeRecoveredResultTimestamp(
        DateTimeOffset executionCompletedAtUtc,
        DateTimeOffset claimExpiresAtUtc)
    {
        var completedAtUtc = NormalizeUtc(executionCompletedAtUtc);
        var expiresAtUtc = NormalizeUtc(claimExpiresAtUtc);
        return completedAtUtc < expiresAtUtc
            ? completedAtUtc
            : expiresAtUtc.AddTicks(-1);
    }

    private RuntimeCommandContext CreateContext(
        string requestedBy,
        DateTimeOffset occurredAtUtc)
    {
        return new RuntimeCommandContext(
            RuntimeCommandId.New(),
            new ProcessEventActor(ProcessEventActorKind.System, new ProcessActorId(requestedBy)),
            new ProcessCorrelationId($"{requestedBy}-{Guid.NewGuid():N}"),
            NormalizeUtc(occurredAtUtc));
    }

    private RuntimeCommandContext CreateContext(string requestedBy)
    {
        return CreateContext(requestedBy, clock.GetUtcNow());
    }

    private static Guid CreateDeterministicGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private static string ComputeHash(string value)
        => "sha256:" +
           Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
               .ToLowerInvariant();

    private static string NormalizeRequestedBy(string requestedBy)
        => string.IsNullOrWhiteSpace(requestedBy)
            ? "agent-execution-recovery"
            : requestedBy.Trim();

    internal static bool CanAssociateClaimWithRecoveredExecution(
        DateTimeOffset claimCreatedAtUtc,
        DateTimeOffset claimExpiresAtUtc,
        DateTimeOffset executionCreatedAtUtc)
    {
        var normalizedClaimCreatedAtUtc = NormalizeUtc(claimCreatedAtUtc);
        var normalizedClaimExpiresAtUtc = NormalizeUtc(claimExpiresAtUtc);
        var normalizedExecutionCreatedAtUtc = NormalizeUtc(executionCreatedAtUtc);
        return normalizedExecutionCreatedAtUtc >= normalizedClaimCreatedAtUtc &&
               normalizedExecutionCreatedAtUtc < normalizedClaimExpiresAtUtc;
    }

    internal static bool IsExecutionBoundToClaim(
        ExecutionRunRecord executionRun,
        Guid claimToken)
    {
        ArgumentNullException.ThrowIfNull(executionRun);

        return claimToken != Guid.Empty &&
               ProcessDispatchClaimExecutionMetadata.Matches(
                   executionRun,
                   new ProcessDispatchClaimIdentity(claimToken));
    }

    internal static bool IsExecutionForProcessStep(
        ExecutionRunRecord executionRun,
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId)
    {
        ArgumentNullException.ThrowIfNull(executionRun);

        return Guid.TryParse(executionRun.ProcessRunId, out var recordedRunId) &&
               recordedRunId == runId.Value &&
               Guid.TryParse(executionRun.ProcessStepId, out var recordedStepInstanceId) &&
               recordedStepInstanceId == stepInstanceId.Value;
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value)
        => value.Offset == TimeSpan.Zero ? value : value.ToUniversalTime();
}

