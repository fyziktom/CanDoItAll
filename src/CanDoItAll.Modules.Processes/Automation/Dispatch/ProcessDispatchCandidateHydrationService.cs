using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessDispatchCandidateHydrationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IWorkspacePathResolver workspacePathResolver,
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
    IAiTechnicalAgentBridge technicalAgentBridge,
    IProcessAutomationExecutionClient executionClient,
    IClock clock,
    TimeSpan staleAutomationExecutionRunTimeout,
    ILogger<ProcessRunAutomationDispatchService> logger)
{
    public async Task<ProcessRouteCandidate?> LoadRouteCandidateAsync(
        Guid processRunId,
        Guid claimedStepRunId,
        string trigger,
        CancellationToken cancellationToken)
    {
        var candidate = await LoadAsync(
            processRunId,
            claimedStepRunId,
            trigger,
            cancellationToken);

        return candidate is null
            ? null
            : ProcessDispatchRouteModelAdapters.FromDispatcherCandidate(candidate);
    }

    public async Task<ProcessRunAutomationDispatchService.DispatchCandidate?> LoadAsync(
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
            var expectedArtifacts = await ProcessDispatchExpectedArtifactLoader.LoadAsync(
                dbContext,
                stepRun.StepDefinitionId,
                cancellationToken);
            var recordedArtifactExpectationIds = snapshot.ExistingArtifacts
                .Where(item => item.StepRunId == stepRun.Id && item.ArtifactExpectationId.HasValue)
                .Select(item => item.ArtifactExpectationId!.Value)
                .ToHashSet();
            var preparedArtifactInputs = ProcessDispatchManagedArtifactPromptPathPreparer.PrepareArtifactInputsForPrompt(
                ProcessDispatchArtifactInputAssembler.BuildResolvedArtifactInputs(
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
            var workflowAssignment = ProcessDispatchAssignmentRouteHelper.ResolveCurrentAssignment(
                stepRun,
                workflowStepRoleRequirements ?? [],
                snapshot.RunAssignments);
            var workflowRole = workflowAssignment is null
                ? null
                : snapshot.RoleRequirementsById.GetValueOrDefault(workflowAssignment.RoleRequirementId);
            if (ProcessDispatchAssignmentRouteHelper.IsWorkflowDispatchAssignment(workflowAssignment, workflowRole))
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
            if (ProcessAutomationExecutionRunSelection.HasBlockingAutomationExecutionRun(
                    executionRuns,
                    clock.GetUtcNow(),
                    ProcessRunAutomationDispatchService.AutomationActor,
                    staleAutomationExecutionRunTimeout))
            {
                continue;
            }

            var recoveryExecutionRunId = ProcessDispatchRecoveryQueryHelper.ResolveRecoverableExecutionRunId(stepRun, executionRuns);
            Guid? reusableChatSessionId = null;
            var manualRecoveryDirective = await ProcessDispatchRecoveryQueryHelper.LoadLatestManualRecoveryDirectiveAsync(
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
                    ProcessExecutionArtifactMetadataRules.BuildMissingTechnicalAgentBindingDiagnostic(
                        run.Id,
                        stepRun.Id,
                        stepRun.Title,
                        executorPartyId,
                        bindingResult.BindingStatus,
                        bindingResult.TechnicalAgentId));
                continue;
            }

            if (bindingResult.Outcome == ProcessDispatchTechnicalAgentBindingOutcome.ProjectStructureAccessGrantedAndSaved &&
                ProcessDispatchTechnicalAgentBindingCoordinator.TryResolveProjectStructureAccessProjectId(run, out var projectStructureAccessProjectId))
            {
                logger.LogInformation(
                    "Granted project-structure read access for project {ProjectId} to technical agent {TechnicalAgentId} before dispatching process run {RunId}, step {StepRunId}.",
                    projectStructureAccessProjectId,
                    technicalAgentId,
                    run.Id,
                    stepRun.Id);
            }

            snapshot.StepRoleRequirementsByStepDefinitionId.TryGetValue(stepRun.StepDefinitionId, out var currentStepRoleRequirements);
            var currentAssignment = ProcessDispatchAssignmentRouteHelper.ResolveCurrentAssignment(
                stepRun,
                currentStepRoleRequirements ?? [],
                snapshot.RunAssignments);
            var currentRole = currentAssignment is null
                ? null
                : snapshot.RoleRequirementsById.GetValueOrDefault(currentAssignment.RoleRequirementId);
            if (ProcessRunAutomationDispatchService.ShouldReusePriorArtifactRecoveryExecutionRun(trigger))
            {
                recoveryExecutionRunId ??= ProcessRunAutomationDispatchService.ResolveArtifactRecoveryExecutionRunId(
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
                    ProcessDispatchCooperationMetadataResolver.ResolveProcessCooperationMetadata(
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
}
