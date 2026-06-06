using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    public async Task DispatchAsync(
        Guid processRunId,
        Guid? triggerStepRunId,
        string trigger,
        Func<CancellationToken, Task>? renewLeaseAsync = null,
        CancellationToken cancellationToken = default)
    {
        if (processRunId == Guid.Empty)
        {
            return;
        }

        var claimCoordinator = CreateDispatchClaimCoordinator();
        while (!cancellationToken.IsCancellationRequested)
        {
            var candidateHeaderLoadStarted = Stopwatch.GetTimestamp();
            var candidateHeaders = await LoadDispatchCandidateHeadersAsync(processRunId, cancellationToken);
            logger.LogDebug(
                "Loaded {CandidateCount} dispatch candidate headers for process run {ProcessRunId} in {ElapsedMilliseconds} ms.",
                candidateHeaders.Count,
                processRunId,
                GetElapsedMilliseconds(candidateHeaderLoadStarted));
            if (candidateHeaders.Count == 0)
            {
                return;
            }

            foreach (var candidateHeader in candidateHeaders)
            {
                using var dispatchGuard = await ProcessDispatchGuardLease.WaitAsync(
                    candidateHeader.StepRunId,
                    StepDispatchGuards,
                    cancellationToken);
                var dispatchClaim = await TryClaimStepDispatchAsync(
                    claimCoordinator,
                    processRunId,
                    candidateHeader.StepRunId,
                    trigger,
                    triggerStepRunId,
                    cancellationToken);
                if (dispatchClaim is null)
                {
                    continue;
                }

                dispatchGuard.Release();

                var dispatchResult = await RunClaimedDispatchAsync(
                    claimCoordinator,
                    processRunId,
                    triggerStepRunId,
                    trigger,
                    dispatchClaim,
                    renewLeaseAsync,
                    cancellationToken);
                if (dispatchResult == ProcessClaimedDispatchResult.ContinueCandidates)
                {
                    continue;
                }

                return;
            }

            return;
        }
    }

    private static double GetElapsedMilliseconds(long startTimestamp)
        => Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

    internal async Task HandleWorkflowExecutionOutcomeAsync(
        DispatchCandidate candidate,
        ProcessWorkflowExecutionOutcome workflowOutcome,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var finalizedCompletion = await FinalizeStepCompletionAsync(
            ProcessDispatchFinalizerContextFactory.ForWorkflow(
                candidate,
                workflowOutcome),
            dispatchClaim,
            cancellationToken);
        if (finalizedCompletion is null)
        {
            return;
        }

        await ApplyFinalizedStepTransitionAsync(candidate, finalizedCompletion, dispatchClaim, cancellationToken);
    }

    internal sealed record ProcessStepDispatchClaim(Guid StepRunId, string ClaimToken);

    internal sealed record DispatchCandidateHeader(Guid StepRunId, ProcessStepRunStatus Status);

    internal async Task<bool> IsRunClosedToAutomationAsync(
        Guid processRunId,
        Guid stepRunId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .Where(run => run.Id == processRunId)
            .Join(
                dbContext.Set<ProcessStepRun>().AsNoTracking().Where(stepRun => stepRun.Id == stepRunId),
                run => run.Id,
                stepRun => stepRun.ProcessRunId,
                (run, stepRun) => new
                {
                    RunStatus = (ProcessRunStatus?)run.Status,
                    StepStatus = (ProcessStepRunStatus?)stepRun.Status
                })
            .SingleOrDefaultAsync(cancellationToken);

        return state is null || IsRunClosedToAutomation(state.RunStatus, state.StepStatus);
    }

    internal static bool IsRunClosedToAutomation(
        ProcessRunStatus? runStatus,
        ProcessStepRunStatus? stepStatus)
    {
        return ProcessDispatchRouteEligibility.IsRunClosedToAutomation(runStatus, stepStatus);
    }

    internal static bool IsRunEligibleForDispatchCandidate(ProcessRunStatus? runStatus)
    {
        return ProcessDispatchRouteEligibility.IsRunEligibleForDispatchCandidate(runStatus);
    }

    internal static bool IsStepStatusDispatchableForRun(
        ProcessRunStatus runStatus,
        ProcessStepRunStatus stepStatus)
    {
        return ProcessDispatchRouteEligibility.IsStepStatusDispatchableForRun(runStatus, stepStatus);
    }

    private ProcessAutomationDatabaseRequirementFailure? ResolveAutomationDatabaseRequirementFailure()
    {
        if (!processRuntimeOptions.Value.RequirePostgreSqlForAgentAutomation)
        {
            return null;
        }

        var profile = databaseProfileRuntimeAccessor.ResolveCurrentProfile();
        if (profile.Profile.ProviderKind == DatabaseProviderKind.PostgreSql)
        {
            return null;
        }

        return new ProcessAutomationDatabaseRequirementFailure(
            $"Governed process automation requires PostgreSQL, but the active database profile is '{profile.Profile.DisplayName}' ({profile.Profile.Id:D}, provider {profile.Profile.ProviderKind}, source {profile.Profile.SourceKind}, resolved by {profile.ResolutionSource}). Switch the active database profile to PostgreSQL before rerunning automation.");
    }

    internal async Task HandleSubprocessDispatchAsync(
        DispatchCandidate candidate,
        string trigger,
        Guid? triggerStepRunId,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var stepRunSnapshot = candidate.StepRun;
        if (stepRunSnapshot.Status != ProcessStepRunStatus.InProgress)
        {
            var startResult = await TransitionStepWithClaimAsync(
                ProcessSubprocessLifecycleRules.BuildStartTransitionRequest(
                    stepRunSnapshot,
                    NormalizeTrigger(trigger, triggerStepRunId),
                    AutomationActor),
                dispatchClaim,
                cancellationToken);
            if (startResult.IsFailure)
            {
                logger.LogInformation(
                    "Process subprocess step {StepRunId} could not be claimed on run {RunId}. Errors: {Errors}",
                    stepRunSnapshot.Id,
                    candidate.Run.Id,
                    string.Join(" | ", startResult.Errors.Select(error => error.Message)));
                return;
            }
        }

        var subprocessResult = await CreateSubprocessRunObservationCoordinator()
            .EnsureRunForStepAsync(stepRunSnapshot.Id, cancellationToken);
        if (subprocessResult.IsFailure)
        {
            await TransitionStepWithClaimAsync(
                ProcessSubprocessLifecycleRules.BuildEnsureFailureBlockTransitionRequest(
                    stepRunSnapshot,
                    string.Join(" | ", subprocessResult.Errors.Select(error => error.Message)),
                    AutomationActor),
                dispatchClaim,
                cancellationToken);
            return;
        }

        var subprocessRun = subprocessResult.Value!;
        var terminalStatus = ProcessSubprocessLifecycleRules.ResolveParentStepStatus(subprocessRun.Status);
        if (!terminalStatus.HasValue)
        {
            var capabilityGapBlockReason = await CreateSubprocessCapabilityGapInspector()
                .TryBuildBlockReasonAsync(subprocessRun, cancellationToken);
            if (capabilityGapBlockReason is not null)
            {
                var blockResult = await TransitionStepWithClaimAsync(
                    ProcessSubprocessLifecycleRules.BuildCapabilityGapBlockTransitionRequest(
                        stepRunSnapshot,
                        capabilityGapBlockReason,
                        AutomationActor),
                    dispatchClaim,
                    cancellationToken);
                if (blockResult.IsFailure)
                {
                    logger.LogWarning(
                        "Subprocess step {StepRunId} on run {RunId} could not be blocked after child run {SubprocessRunId} exposed capability gaps. Errors: {Errors}",
                        stepRunSnapshot.Id,
                        candidate.Run.Id,
                        subprocessRun.RunId,
                        string.Join(" | ", blockResult.Errors.Select(error => error.Message)));
                }

                return;
            }

            logger.LogInformation(
                "Subprocess step {StepRunId} on run {RunId} is observing child run {SubprocessRunId} with status {SubprocessStatus}.",
                stepRunSnapshot.Id,
                candidate.Run.Id,
                subprocessRun.RunId,
                subprocessRun.Status);
            return;
        }

        if (terminalStatus.Value == ProcessStepRunStatus.Completed)
        {
            await ProjectCompletedSubprocessArtifactsAsync(candidate, subprocessRun, dispatchClaim, cancellationToken);
            var transitionReason = ProcessSubprocessLifecycleRules.BuildParentTransitionReason(subprocessRun);
            var finalizedCompletion = await FinalizeStepCompletionAsync(
                ProcessDispatchFinalizerContextFactory.ForSubprocess(
                    candidate,
                    subprocessRun.RunId,
                    terminalStatus.Value,
                    transitionReason),
                dispatchClaim,
                cancellationToken);
            if (finalizedCompletion is not null)
            {
                await ApplyFinalizedStepTransitionAsync(candidate, finalizedCompletion, dispatchClaim, cancellationToken);
            }

            return;
        }

        var transitionResult = await TransitionStepWithClaimAsync(
            ProcessSubprocessLifecycleRules.BuildTerminalMirrorTransitionRequest(
                stepRunSnapshot,
                subprocessRun,
                terminalStatus.Value,
                AutomationActor),
            dispatchClaim,
            cancellationToken);
        if (transitionResult.IsFailure)
        {
            logger.LogWarning(
                "Subprocess step {StepRunId} on run {RunId} could not mirror child run {SubprocessRunId}. Errors: {Errors}",
                stepRunSnapshot.Id,
                candidate.Run.Id,
                subprocessRun.RunId,
                string.Join(" | ", transitionResult.Errors.Select(error => error.Message)));
        }
    }

    private async Task ProjectCompletedSubprocessArtifactsAsync(
        DispatchCandidate candidate,
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await EnsureStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var expectations = await dbContext.Set<ProcessArtifactExpectation>()
            .Where(item =>
                item.StepDefinitionId == candidate.StepRun.StepDefinitionId &&
                item.IsRequired)
            .OrderBy(item => item.Title)
            .ToListAsync(cancellationToken);
        if (expectations.Count == 0)
        {
            return;
        }

        var parentArtifacts = await dbContext.Set<ProcessArtifactRecord>()
            .Where(item =>
                item.ProcessRunId == candidate.Run.Id &&
                item.StepRunId == candidate.StepRun.Id)
            .ToListAsync(cancellationToken);
        var missingProjectableExpectations = expectations
            .Where(ProcessSubprocessArtifactSourceResolver.IsCompletionProjectionAllowed)
            .Where(expectation => !parentArtifacts.Any(artifact =>
                ProcessSubprocessProjectionPlanBuilder.SatisfiesCurrentArtifactExpectation(
                    artifact,
                    expectation,
                    subprocessRun.RunId)))
            .ToList();
        if (missingProjectableExpectations.Count == 0)
        {
            return;
        }

        var childArtifacts = await dbContext.Set<ProcessArtifactRecord>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == subprocessRun.RunId)
            .ToListAsync(cancellationToken);
        childArtifacts = childArtifacts
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
        var now = clock.GetUtcNow();
        var scopedProfileId = databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N");
        var gapJournalCoordinator = CreateSubprocessProjectionGapJournalCoordinator();
        var projectionWriterCoordinator = CreateSubprocessProjectionWriterCoordinator();

        foreach (var expectation in missingProjectableExpectations)
        {
            await EnsureStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken);
            var sourceArtifact = ProcessSubprocessArtifactSourceResolver.ResolveSourceArtifact(
                childArtifacts,
                missingProjectableExpectations,
                expectation,
                out var projectionDiagnostic);
            if (sourceArtifact is null)
            {
                await gapJournalCoordinator.RecordAsync(
                    dbContext,
                    candidate,
                    subprocessRun,
                    expectation,
                    projectionDiagnostic,
                    now,
                    cancellationToken);
                continue;
            }

            var projectionPlan = ProcessSubprocessProjectionPlanBuilder.Build(
                candidate,
                subprocessRun,
                expectation,
                sourceArtifact,
                projectionDiagnostic,
                scopedProfileId);
            await projectionWriterCoordinator.WriteAsync(
                dbContext,
                candidate,
                subprocessRun,
                projectionPlan,
                now,
                cancellationToken);
        }

        await EnsureStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    internal static ProcessArtifactRecord? ResolveSubprocessSourceArtifact(
        IReadOnlyList<ProcessArtifactRecord> childArtifacts,
        IReadOnlyList<ProcessArtifactExpectation> parentExpectations,
        ProcessArtifactExpectation expectation,
        out string diagnostic)
    {
        return ProcessSubprocessArtifactSourceResolver.ResolveSourceArtifact(
            childArtifacts,
            parentExpectations,
            expectation,
            out diagnostic);
    }

    internal static IReadOnlyList<ProcessSubprocessOutputArtifactMapping> ResolveSubprocessOutputArtifactMappings(
        IReadOnlyList<ProcessArtifactExpectation> parentExpectations)
    {
        return ProcessSubprocessArtifactSourceResolver.ResolveOutputArtifactMappings(parentExpectations);
    }

    private ProcessSubprocessRunObservationCoordinator CreateSubprocessRunObservationCoordinator()
        => new(serviceScopeFactory);

    private ProcessSubprocessCapabilityGapInspector CreateSubprocessCapabilityGapInspector()
        => new(dbContextFactory);

    private static ProcessSubprocessProjectionGapJournalCoordinator CreateSubprocessProjectionGapJournalCoordinator()
        => new();

    private ProcessSubprocessProjectionWriterCoordinator CreateSubprocessProjectionWriterCoordinator()
        => new(workspacePathResolver);

    private async Task BlockDispatchForDatabaseRequirementAsync(
        DispatchCandidate candidate,
        ProcessAutomationDatabaseRequirementFailure failure,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var decision = CreatePreExecutionGuardHandler().BuildDatabaseRequirementDecision(
            candidate,
            failure.Message,
            AutomationActor);
        if (decision.IsUnsupportedNoOpTarget)
        {
            logger.LogWarning(
                "Process automation dispatch for run {RunId}, step {StepRunId} requires PostgreSQL but current status {Status} has no supported blocking transition. Reason: {Reason}",
                candidate.Run.Id,
                candidate.StepRun.Id,
                candidate.StepRun.Status,
                failure.Message);
            return;
        }

        if (!decision.IsTransitionAllowed)
        {
            logger.LogWarning(
                "Process automation dispatch for run {RunId}, step {StepRunId} requires PostgreSQL but current status {Status} cannot transition to {TargetStatus}. Reason: {Reason}",
                candidate.Run.Id,
                candidate.StepRun.Id,
                candidate.StepRun.Status,
                decision.TargetStatus,
                failure.Message);
            return;
        }

        var transitionRequest = decision.TransitionRequest
            ?? throw new InvalidOperationException("Database requirement transition request was not built for a supported target.");
        var transitionResult = await TransitionStepWithClaimAsync(
            transitionRequest,
            dispatchClaim,
            cancellationToken);

        if (transitionResult.IsFailure)
        {
            logger.LogWarning(
                "Process step {StepRunId} could not be moved to {TargetStatus} after PostgreSQL runtime requirement failed. Errors: {Errors}",
                candidate.StepRun.Id,
                decision.TargetStatus,
                string.Join(" | ", transitionResult.Errors.Select(error => error.Message)));
            return;
        }

        logger.LogWarning(
            "Blocked process automation dispatch for run {RunId}, step {StepRunId} because the active database profile is not PostgreSQL.",
            candidate.Run.Id,
            candidate.StepRun.Id);
    }

    internal async Task<bool> TryRequestMissingUpstreamArtifactMaterializationAsync(
        DispatchCandidate candidate,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var handler = CreatePreExecutionGuardHandler();
        var plan = handler.PlanMissingUpstreamArtifactMaterialization(candidate);
        if (!plan.HasMissingInputs)
        {
            return false;
        }

        if (candidate.StepRun.Status != ProcessStepRunStatus.Blocked)
        {
            var snapshot = await LoadStepRunTransitionSnapshotAsync(candidate.StepRun.Id, cancellationToken);
            if (snapshot is not null &&
                snapshot.Status is ProcessStepRunStatus.Ready or ProcessStepRunStatus.WaitingApproval or ProcessStepRunStatus.InProgress)
            {
                var blockResult = await TransitionStepWithClaimAsync(
                    handler.BuildMissingUpstreamArtifactBlockTransitionRequest(
                        plan,
                        candidate.StepRun.Id,
                        snapshot.ConcurrencyToken,
                        AutomationActor),
                    dispatchClaim,
                    cancellationToken);
                if (blockResult.IsFailure)
                {
                    logger.LogWarning(
                        "Could not block downstream step {StepRunId} before upstream artifact materialization for run {RunId}. Errors: {Errors}",
                        candidate.StepRun.Id,
                        candidate.Run.Id,
                        string.Join(" | ", blockResult.Errors.Select(error => error.Message)));
                    return true;
                }
            }
        }

        return await handler.RecordAndRequestMissingUpstreamArtifactMaterializationAsync(
            candidate,
            plan,
            cancellationToken);
    }

    private async Task<bool> RecordMissingUpstreamArtifactMaterializationAsync(
        DispatchCandidate candidate,
        IReadOnlyList<DispatchArtifactInput> missingInputs,
        DispatchArtifactInput? materializationTarget,
        string blockReason,
        CancellationToken cancellationToken)
    {
        var facts = new ProcessMissingUpstreamArtifactMaterializationFacts(missingInputs, materializationTarget);
        return await CreateMissingUpstreamArtifactMaterializationJournalCoordinator()
            .RecordAsync(candidate, facts, blockReason, cancellationToken);
    }

    private static string CreateMissingUpstreamArtifactMaterializationFingerprint(
        DispatchCandidate candidate,
        IReadOnlyList<DispatchArtifactInput> missingInputs,
        DispatchArtifactInput? materializationTarget)
    {
        return ProcessMissingUpstreamArtifactMaterializationFingerprint.Create(
            candidate,
            new ProcessMissingUpstreamArtifactMaterializationFacts(missingInputs, materializationTarget));
    }

    private static IReadOnlyList<DispatchArtifactInput> ResolveMissingUpstreamArtifactInputs(DispatchCandidate candidate)
    {
        return ProcessMissingUpstreamArtifactMaterializationFactsResolver.ResolveMissingInputs(candidate);
    }

    private static bool IsRunnableUpstreamArtifactMaterializationTarget(DispatchArtifactInput input)
    {
        return ProcessMissingUpstreamArtifactMaterializationFactsResolver.IsRunnableTarget(input);
    }

    private static string BuildMissingUpstreamArtifactMaterializationBlockReason(
        DispatchCandidate candidate,
        IReadOnlyList<DispatchArtifactInput> missingInputs,
        DispatchArtifactInput? materializationTarget)
    {
        return ProcessMissingUpstreamArtifactMaterializationBlocker.BuildBlockReason(
            candidate,
            new ProcessMissingUpstreamArtifactMaterializationFacts(missingInputs, materializationTarget));
    }

    private static string BuildUpstreamArtifactMaterializationDirective(
        DispatchCandidate candidate,
        IReadOnlyList<DispatchArtifactInput> missingInputs,
        DispatchArtifactInput materializationTarget)
    {
        return ProcessMissingUpstreamArtifactRerunRequestBuilder.BuildDirective(
            candidate,
            new ProcessMissingUpstreamArtifactMaterializationFacts(missingInputs, materializationTarget));
    }

    private ProcessMissingUpstreamArtifactMaterializationJournalCoordinator CreateMissingUpstreamArtifactMaterializationJournalCoordinator()
    {
        return new ProcessMissingUpstreamArtifactMaterializationJournalCoordinator(dbContextFactory, clock);
    }

    private ProcessDispatchPreExecutionGuardHandler CreatePreExecutionGuardHandler()
    {
        return new ProcessDispatchPreExecutionGuardHandler(
            new ProcessMissingUpstreamArtifactMaterializationCoordinator(
                CreateMissingUpstreamArtifactMaterializationJournalCoordinator(),
                serviceScopeFactory,
                logger));
    }

    private async Task<IReadOnlyList<DispatchCandidateHeader>> LoadDispatchCandidateHeadersAsync(
        Guid processRunId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await ProcessDispatchCandidateHeaderSelector.SelectAsync(
            dbContext,
            processRunId,
            clock.GetUtcNow(),
            cancellationToken);
    }

    internal async Task<DispatchCandidate?> LoadDispatchCandidateAsync(
        Guid processRunId,
        Guid claimedStepRunId,
        string trigger,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var snapshot = await ProcessDispatchCandidateHydrationLoader.LoadAsync(
            dbContext,
            processRunId,
            claimedStepRunId,
            cancellationToken);
        if (snapshot is null)
        {
            return null;
        }

        var run = snapshot.Run;
        var definition = snapshot.Definition;
        var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        var workspaceScope = WorkspaceScopeDescriptor.Organization(
            databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N"));

        foreach (var stepRun in snapshot.DispatchableSteps)
        {
            if (!snapshot.ReadyStepDefinitionsById.TryGetValue(stepRun.StepDefinitionId, out var currentStepDefinition))
            {
                continue;
            }

            snapshot.ArtifactInputsByStepDefinitionId.TryGetValue(stepRun.StepDefinitionId, out var configuredArtifactInputs);
            var branchContext = ProcessDispatchBranchDependencyContext.Create(
                stepRun,
                snapshot.BranchOutcomesByStepDefinitionId,
                snapshot.ConditionalDependencyOutcomeIdsByStepDefinitionId);
            var expectedArtifacts = await LoadExpectedArtifactsAsync(dbContext, stepRun.StepDefinitionId, cancellationToken);
            var recordedArtifactExpectationIds = snapshot.ExistingArtifacts
                .Where(item => item.StepRunId == stepRun.Id && item.ArtifactExpectationId.HasValue)
                .Select(item => item.ArtifactExpectationId!.Value)
                .ToHashSet();
            var preparedArtifactInputs = PrepareArtifactInputsForPrompt(
                BuildResolvedArtifactInputs(
                    configuredArtifactInputs ?? [],
                    snapshot.ArtifactExpectationsById,
                    snapshot.SourceStepsById,
                    snapshot.StepRunsByDefinitionId,
                    snapshot.ExistingArtifacts),
                workspaceRoot,
                workspaceScope);
            var assemblyContext = ProcessDispatchCandidateAssemblyContextFactory.Create(
                run,
                definition,
                stepRun,
                currentStepDefinition,
                snapshot.WorkBriefsByStepRunId.GetValueOrDefault(stepRun.Id),
                expectedArtifacts,
                recordedArtifactExpectationIds,
                preparedArtifactInputs,
                snapshot.ExternalReferenceKeys,
                branchContext);

            if (stepRun.StepKind == ProcessStepKind.Subprocess)
            {
                return ProcessDispatchCandidateFactory.CreateSubprocessCandidate(assemblyContext);
            }

            snapshot.StepRoleRequirementsByStepDefinitionId.TryGetValue(stepRun.StepDefinitionId, out var workflowStepRoleRequirements);
            var workflowAssignment = ResolveDispatchCurrentAssignment(stepRun, workflowStepRoleRequirements ?? [], snapshot.RunAssignments);
            var workflowRole = workflowAssignment is null
                ? null
                : snapshot.RoleRequirementsById.GetValueOrDefault(workflowAssignment.RoleRequirementId);
            if (IsWorkflowDispatchAssignment(workflowAssignment, workflowRole))
            {
                return ProcessDispatchCandidateFactory.CreateWorkflowCandidate(assemblyContext);
            }

            if (!stepRun.CurrentExecutorPartyId.HasValue)
            {
                continue;
            }

            var executorPartyId = stepRun.CurrentExecutorPartyId.Value;
            var executionRuns = await executionClient.ListExecutionRunsAsync(
                new ProcessAutomationExecutionRunQuery(
                    ProcessRunId: processRunId.ToString("D"),
                    ProcessStepId: stepRun.Id.ToString("D"),
                    Take: 20),
                cancellationToken);
            if (HasBlockingAutomationExecutionRun(executionRuns, clock.GetUtcNow()))
            {
                continue;
            }

            var recoveryExecutionRunId = ProcessDispatchRecoveryQueryHelper.ResolveRecoverableExecutionRunId(stepRun, executionRuns);
            Guid? reusableChatSessionId = null;
            var manualRecoveryDirective = await LoadLatestManualRecoveryDirectiveAsync(
                dbContext,
                run.Id,
                stepRun.Id,
                stepRun.StartedAtUtc,
                cancellationToken);
            var bindingResult = await ProcessDispatchTechnicalAgentBindingCoordinator.ResolveAsync(
                run,
                stepRun,
                executorPartyId,
                technicalAgentBridge,
                executionClient,
                cancellationToken);
            if (bindingResult.TechnicalAgentId is not { } technicalAgentId ||
                bindingResult.AgentEditor is not { } agentEditor)
            {
                logger.LogWarning(
                    "{Diagnostic}",
                    BuildMissingTechnicalAgentBindingDiagnostic(
                        run.Id,
                        stepRun.Id,
                        stepRun.Title,
                        executorPartyId,
                        bindingResult.BindingStatus,
                        bindingResult.TechnicalAgentId));
                continue;
            }

            if (bindingResult.Outcome == ProcessDispatchTechnicalAgentBindingOutcome.ProjectStructureAccessGrantedAndSaved &&
                TryResolveProjectStructureAccessProjectId(run, out var projectStructureAccessProjectId))
            {
                logger.LogInformation(
                    "Granted project-structure read access for project {ProjectId} to technical agent {TechnicalAgentId} before dispatching process run {RunId}, step {StepRunId}.",
                    projectStructureAccessProjectId,
                    technicalAgentId,
                    run.Id,
                    stepRun.Id);
            }

            snapshot.StepRoleRequirementsByStepDefinitionId.TryGetValue(stepRun.StepDefinitionId, out var currentStepRoleRequirements);
            var currentAssignment = ResolveDispatchCurrentAssignment(stepRun, currentStepRoleRequirements ?? [], snapshot.RunAssignments);
            var currentRole = currentAssignment is null
                ? null
                : snapshot.RoleRequirementsById.GetValueOrDefault(currentAssignment.RoleRequirementId);
            if (ShouldReusePriorArtifactRecoveryExecutionRun(trigger))
            {
                recoveryExecutionRunId ??= ResolveArtifactRecoveryExecutionRunId(
                    stepRun,
                    executionRuns,
                    expectedArtifacts,
                    recordedArtifactExpectationIds);
            }

            var directAgentContext = ProcessDispatchCandidateAssemblyContextFactory.WithDirectAgentFacts(
                assemblyContext,
                new ProcessDispatchDirectAgentCandidateFacts(
                    technicalAgentId,
                    reusableChatSessionId,
                    recoveryExecutionRunId,
                    manualRecoveryDirective,
                    ResolveProcessCooperationMetadata(
                        stepRun,
                        assemblyContext.WorkBrief,
                        currentRole,
                        currentAssignment,
                        expectedArtifacts,
                        preparedArtifactInputs,
                        branchContext.BranchOutcomes,
                        agentEditor)));
            return ProcessDispatchCandidateFactory.CreateDirectAgentCandidate(directAgentContext);
        }

        return null;
    }

    internal static bool ApplyProjectStructureReadAccess(AgentEditorModel agentEditor, Guid projectId)
    {
        return ProcessDispatchTechnicalAgentBindingCoordinator.ApplyProjectStructureReadAccess(agentEditor, projectId);
    }

    private static bool TryResolveProjectStructureAccessProjectId(ProcessRun run, out Guid projectId)
    {
        return ProcessDispatchTechnicalAgentBindingCoordinator.TryResolveProjectStructureAccessProjectId(run, out projectId);
    }

    private static bool IsWorkflowDispatchAssignment(
        ProcessRunAssignment? assignment,
        ProcessRoleRequirement? role)
    {
        return ProcessDispatchAssignmentRouteHelper.IsWorkflowDispatchAssignment(assignment, role);
    }

    private static async Task<string> LoadLatestManualRecoveryDirectiveAsync(
        AppDbContext dbContext,
        Guid runId,
        Guid stepRunId,
        DateTimeOffset? stepStartedAtUtc,
        CancellationToken cancellationToken)
    {
        return await ProcessDispatchRecoveryQueryHelper.LoadLatestManualRecoveryDirectiveAsync(
            dbContext,
            runId,
            stepRunId,
            stepStartedAtUtc,
            cancellationToken);
    }

}
