using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessAgentRuntimeToolProvider
{
    private async Task<IReadOnlyList<ProcessRunListItem>> ProcessesRunsListAsync(
        ProcessAccessState accessState,
        Guid? definitionId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        EnsureReadAllowed(accessState);
        if (definitionId.HasValue)
        {
            EnsureDefinitionReadAllowed(accessState, definitionId.Value);
        }

        var runs = await processesService.ListRunsAsync(definitionId, projectId, cancellationToken);
        return accessState.AllowAllDefinitions
            ? runs
            : runs
                .Where(item => accessState.AllowedDefinitionIds.Contains(item.ProcessDefinitionId))
                .ToList();
    }

    private async Task<InternalProcessRunDetailToolData> ProcessesRunDetailGetAsync(
        ProcessAccessState accessState,
        Guid runId,
        CancellationToken cancellationToken)
    {
        EnsureReadAllowed(accessState);
        var run = await GetRunAsync(runId, cancellationToken);
        EnsureDefinitionReadAllowed(accessState, run.ProcessDefinitionId);

        var details = await processesService.GetRunDetailsAsync(runId, cancellationToken);
        var improvements = await processesService.ListRunImprovementsAsync(runId, cancellationToken);

        return new InternalProcessRunDetailToolData(
            run,
            details.Health,
            details.StepRuns,
            details.Decisions,
            details.Artifacts,
            details.Assignments,
            details.WorkBriefs,
            details.ConformanceObservations,
            improvements);
    }

    private async Task<ProcessAnalyticsSummary> ProcessesAnalyticsGetAsync(
        ProcessAccessState accessState,
        Guid? definitionId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        EnsureReadAllowed(accessState);
        if (definitionId.HasValue)
        {
            EnsureDefinitionReadAllowed(accessState, definitionId.Value);
            return await processesService.GetAnalyticsAsync(definitionId, projectId, cancellationToken);
        }

        if (projectId.HasValue)
        {
            await EnsureProjectReadAllowedAsync(accessState, projectId.Value, cancellationToken);
        }

        var allowedDefinitionIds = accessState.AllowAllDefinitions
            ? (await GetAllowedDefinitionsByIdAsync(accessState, cancellationToken)).Keys.ToList()
            : accessState.AllowedDefinitionIds.ToList();
        return await processesService.GetAnalyticsForDefinitionsAsync(
            allowedDefinitionIds,
            projectId,
            cancellationToken);
    }

    private async Task<Guid> ProcessesRunStartAsync(
        ProcessAccessState accessState,
        ProcessRunStartRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWriteAllowed(accessState);
        if (request.LaunchPlanId.HasValue)
        {
            var launchPlan = await processesService.GetLaunchPlanAccessSummaryAsync(request.LaunchPlanId.Value, cancellationToken);
            if (launchPlan is null)
            {
                throw new ProcessToolException(
                    "ProcessLaunchPlanNotFound",
                    $"Process launch plan '{request.LaunchPlanId.Value:D}' was not found.");
            }

            EnsureDefinitionWriteAllowed(accessState, launchPlan.ProcessDefinitionId);
        }
        else if (request.ProcessDefinitionId != Guid.Empty)
        {
            EnsureDefinitionWriteAllowed(accessState, request.ProcessDefinitionId);
        }

        return EnsureSuccess(await processesService.StartRunAsync(request, cancellationToken));
    }

    private async Task<Guid> ProcessesStepTransitionAsync(
        ProcessAccessState accessState,
        ProcessStepTransitionRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWriteAllowed(accessState);
        var stepRun = await processesService.GetStepRunAccessSummaryAsync(request.StepRunId, cancellationToken);
        if (stepRun is null)
        {
            throw new ProcessToolException(
                "ProcessStepRunNotFound",
                $"Process step run '{request.StepRunId:D}' was not found.");
        }

        EnsureDefinitionWriteAllowed(accessState, stepRun.ProcessDefinitionId);
        EnsureSuccess(await processesService.TransitionStepAsync(request, cancellationToken));
        return request.StepRunId;
    }

    private async Task<Guid> ProcessesAssignmentResolveAsync(
        ProcessAccessState accessState,
        ProcessAssignmentResolutionRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWriteAllowed(accessState);
        var run = await GetRunAsync(request.ProcessRunId, cancellationToken);
        EnsureDefinitionWriteAllowed(accessState, run.ProcessDefinitionId);
        EnsureSuccess(await processesService.ResolveAssignmentAsync(request, cancellationToken));
        return request.ProcessRunId;
    }

    private async Task<Guid> ProcessesArtifactRecordAsync(
        ProcessAccessState accessState,
        ProcessArtifactRecordRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWriteAllowed(accessState);
        var run = await GetRunAsync(request.ProcessRunId, cancellationToken);
        EnsureDefinitionWriteAllowed(accessState, run.ProcessDefinitionId);
        return EnsureSuccess(await processesService.RecordArtifactAsync(request, cancellationToken));
    }

    private async Task<IReadOnlyList<ProjectPartyOption>> ProcessesPartyOptionsListAsync(
        ProcessAccessState accessState,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        EnsureReadAllowed(accessState);
        await EnsureProjectReadAllowedAsync(accessState, projectId, cancellationToken);
        return await processesService.ListPartyOptionsAsync(projectId, cancellationToken);
    }

    private async Task<IReadOnlyList<ProcessExecutorRegistryOption>> ProcessesExecutorOptionsListAsync(
        ProcessAccessState accessState,
        CancellationToken cancellationToken)
    {
        EnsureReadAllowed(accessState);
        return await processesService.ListExecutorOptionsAsync(cancellationToken);
    }
}
