using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.Processes;

using DispatchCandidate = ProcessRunAutomationDispatchService.DispatchCandidate;

internal sealed class ProcessDispatchHydratedCandidateAssembler(
    ProcessDispatchCandidateArtifactInputPreparationService artifactInputPreparationService,
    ProcessDispatchDirectAgentCandidateAssembler directAgentCandidateAssembler)
{
    public async Task<DispatchCandidate?> TryAssembleAsync(
        AppDbContext dbContext,
        ProcessDispatchCandidateHydrationSnapshot snapshot,
        string trigger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(snapshot);

        var run = snapshot.Run;
        var definition = snapshot.Definition;

        foreach (var stepRun in snapshot.DispatchableSteps)
        {
            if (!snapshot.ReadyStepDefinitionsById.TryGetValue(stepRun.StepDefinitionId, out var currentStepDefinition))
            {
                continue;
            }

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
            var preparedArtifactInputs = artifactInputPreparationService.Prepare(snapshot, stepRun);
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

            var directAgentCandidate = await directAgentCandidateAssembler.TryCreateAsync(
                dbContext,
                snapshot,
                assemblyContext,
                stepRun,
                trigger,
                expectedArtifacts,
                recordedArtifactExpectationIds,
                preparedArtifactInputs,
                branchContext,
                cancellationToken);
            if (directAgentCandidate is not null)
            {
                return directAgentCandidate;
            }
        }

        return null;
    }
}
