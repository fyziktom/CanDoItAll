using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

using DispatchArtifactExpectation = ProcessRunAutomationDispatchService.DispatchArtifactExpectation;
using DispatchArtifactInput = ProcessRunAutomationDispatchService.DispatchArtifactInput;
using DispatchCandidate = ProcessRunAutomationDispatchService.DispatchCandidate;

internal sealed class ProcessDispatchDirectAgentCandidateAssembler(
    IAiTechnicalAgentBridge technicalAgentBridge,
    IProcessAutomationExecutionClient executionClient,
    IClock clock,
    TimeSpan staleAutomationExecutionRunTimeout,
    ILogger<ProcessRunAutomationDispatchService> logger)
{
    public async Task<DispatchCandidate?> TryCreateAsync(
        AppDbContext dbContext,
        ProcessDispatchCandidateHydrationSnapshot snapshot,
        ProcessDispatchCandidateAssemblyContext assemblyContext,
        ProcessStepRun stepRun,
        string trigger,
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        HashSet<Guid> recordedArtifactExpectationIds,
        IReadOnlyList<DispatchArtifactInput> preparedArtifactInputs,
        ProcessDispatchBranchDependencyContext branchContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(assemblyContext);
        ArgumentNullException.ThrowIfNull(stepRun);
        ArgumentNullException.ThrowIfNull(expectedArtifacts);
        ArgumentNullException.ThrowIfNull(recordedArtifactExpectationIds);
        ArgumentNullException.ThrowIfNull(preparedArtifactInputs);
        ArgumentNullException.ThrowIfNull(branchContext);

        if (!stepRun.CurrentExecutorPartyId.HasValue)
        {
            return null;
        }

        var run = snapshot.Run;
        var executorPartyId = stepRun.CurrentExecutorPartyId.Value;
        var executionRuns = await executionClient.ListExecutionRunsAsync(
            new ProcessAutomationExecutionRunQuery(
                ProcessRunId: run.Id.ToString("D"),
                ProcessStepId: stepRun.Id.ToString("D"),
                Take: 20),
            cancellationToken);
        if (ProcessAutomationExecutionRunSelection.HasBlockingAutomationExecutionRun(
                executionRuns,
                clock.GetUtcNow(),
                ProcessRunAutomationDispatchService.AutomationActor,
                staleAutomationExecutionRunTimeout))
        {
            return null;
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
            return null;
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
}
