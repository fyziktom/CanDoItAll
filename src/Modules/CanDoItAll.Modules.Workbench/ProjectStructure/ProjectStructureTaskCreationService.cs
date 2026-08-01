using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureTaskCreationService(
    ProjectStructureAgentService agentService,
    ProjectStructureTaskResourceService resourceService,
    ProjectStructureGanttRowOrderService rowOrderService,
    ProjectWorkbenchService projectWorkbenchService,
    ProjectStructureTaskEstimateRefreshService estimateRefreshService,
    ILogger<ProjectStructureTaskCreationService> logger)
{
    private const string BacklogSubtype = "backlog";
    private const string MainBacklogTitle = "Main";
    private const int MaximumTaskTitleLength = 200;
    private const string TaskSubtype = "task";

    public Task<ProjectStructureTaskCreateResult> CreateAsync(
        Guid projectId,
        ProjectStructureTaskCreateRequest request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(agent);
        var normalizedRequest = ValidateAndNormalize(projectId, request);

        return rowOrderService.RunProjectMutationAsync(
            projectId,
            agent,
            "create-gantt-task",
            innerCancellationToken => CreateCoreAsync(projectId, normalizedRequest, agent, innerCancellationToken),
            cancellationToken);
    }

    private async Task<ProjectStructureTaskCreateResult> CreateCoreAsync(
        Guid projectId,
        ProjectStructureTaskCreateRequest request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken)
    {
        var pricing = await estimateRefreshService.RefreshAsync(
            projectId,
            ProjectTaskExecutionState.NotStarted,
            request.Resource,
            request.Estimate ?? ProjectTaskEstimate.Empty(),
            ProjectStructureTaskMissingResourcePricingPolicy.PreserveManualEstimate,
            cancellationToken);
        var backlog = await EnsureMainBacklogAsync(projectId, agent, cancellationToken);
        var metadata = new ProjectObjectMetadataEnvelope
        {
            WorkItem = new ProjectWorkItemMetadata
            {
                WorkItemKind = ProjectWorkItemKind.Task,
                ExecutionState = ProjectTaskExecutionState.NotStarted,
                Description = request.Title,
                ExpectedEffortHours = pricing.Estimate.ExpectedEffortHours,
                ExpectedEffortUnit = pricing.Estimate.ExpectedEffortUnit,
                ExpectedCostAmount = pricing.Estimate.ExpectedCostAmount,
                ExpectedCostCurrencyCode = pricing.Estimate.ExpectedCostCurrencyCode,
                ExpectedCostBasis = pricing.CalculatedCostBasis
            }
        };
        var task = await agentService.CreateCanonicalTaskNodeAsync(
            projectId,
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.WorkItem,
                request.Title,
                "Task",
                string.Empty,
                backlog.Id,
                StartUtc: request.StartUtc,
                EndUtc: request.EndUtc,
                ObjectSubtype: TaskSubtype,
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(metadata),
                DurationSeconds: Math.Max(
                    1,
                    checked((int)Math.Round(request.Duration.TotalSeconds, MidpointRounding.AwayFromZero)))),
            agent,
            cancellationToken);

        if (request.Resource is not null)
        {
            try
            {
                await resourceService.AttachAsync(
                    projectId,
                    task.Id,
                    request.Resource,
                    agent,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await CompensateCancellationAsync(
                    projectId,
                    task.Id,
                    ProjectStructureTaskCreationFailureStage.ResourceAttachment,
                    agent);
                throw;
            }
            catch (Exception failure)
            {
                throw await CompensateAsync(
                    projectId,
                    task.Id,
                    ProjectStructureTaskCreationFailureStage.ResourceAttachment,
                    failure,
                    agent);
            }
        }

        try
        {
            await rowOrderService.InsertWithinProjectMutationAsync(
                projectId,
                task.Id,
                request.AfterTaskNodeId,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await CompensateCancellationAsync(
                projectId,
                task.Id,
                ProjectStructureTaskCreationFailureStage.RowOrdering,
                agent);
            throw;
        }
        catch (Exception failure)
        {
            throw await CompensateAsync(
                projectId,
                task.Id,
                ProjectStructureTaskCreationFailureStage.RowOrdering,
                failure,
                agent);
        }

        return new ProjectStructureTaskCreateResult(task.Id, backlog.Id, request.Resource, pricing);
    }

    private async Task<ProjectStructureNodeSummary> EnsureMainBacklogAsync(
        Guid projectId,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var rootNodeId = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId);
        var existing = surface.Nodes.FirstOrDefault(node =>
            !node.IsSystemManaged &&
            string.Equals(node.ParentId, rootNodeId, StringComparison.Ordinal) &&
            node.ObjectType == ProjectObjectType.ProjectBlock &&
            string.Equals(node.ObjectSubtype, BacklogSubtype, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(node.Title, MainBacklogTitle, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return ProjectStructureAgentService.MapNodeSummaryForInternalUse(existing);
        }

        return await agentService.CreateNodeAsync(
            projectId,
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.ProjectBlock,
                MainBacklogTitle,
                "Unsorted tasks",
                "Tasks created from the Gantt projection before they are organized in the project structure.",
                rootNodeId,
                ObjectSubtype: BacklogSubtype,
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    ProjectBlock = new ProjectBlockMetadata()
                })),
            agent,
            cancellationToken);
    }

    private async Task<ProjectStructureTaskCreationException> CompensateAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskCreationFailureStage stage,
        Exception failure,
        ProjectStructureAgentContext agent)
    {
        logger.LogWarning(
            failure,
            "Gantt task creation failed. ProjectId={ProjectId} TaskNodeId={TaskNodeId} Stage={Stage}",
            projectId,
            taskNodeId,
            stage);

        var compensationFailure = await TryRemoveCreatedTaskAsync(projectId, taskNodeId, agent);
        LogCompensationFailure(projectId, taskNodeId, stage, compensationFailure);

        return new ProjectStructureTaskCreationException(
            stage,
            taskNodeId,
            compensationFailure is null,
            failure,
            compensationFailure);
    }

    private async Task CompensateCancellationAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskCreationFailureStage stage,
        ProjectStructureAgentContext agent)
    {
        logger.LogInformation(
            "Gantt task creation was canceled. Removing the partially created task. ProjectId={ProjectId} TaskNodeId={TaskNodeId} Stage={Stage}",
            projectId,
            taskNodeId,
            stage);

        var compensationFailure = await TryRemoveCreatedTaskAsync(projectId, taskNodeId, agent);
        LogCompensationFailure(projectId, taskNodeId, stage, compensationFailure);
    }

    private async Task<Exception?> TryRemoveCreatedTaskAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureAgentContext agent)
    {
        try
        {
            var deletedCount = await agentService.DeleteNodeAsync(
                projectId,
                taskNodeId,
                new ProjectStructureNodeDeleteInput(),
                agent,
                CancellationToken.None);
            if (deletedCount == 0)
            {
                return new InvalidOperationException(
                    $"Compensation did not find task '{taskNodeId}' in project '{projectId:D}'.");
            }
        }
        catch (Exception exception)
        {
            return exception;
        }

        return null;
    }

    private void LogCompensationFailure(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskCreationFailureStage stage,
        Exception? compensationFailure)
    {
        if (compensationFailure is null)
        {
            return;
        }

        logger.LogError(
            compensationFailure,
            "Gantt task creation compensation failed. ProjectId={ProjectId} TaskNodeId={TaskNodeId} Stage={Stage}",
            projectId,
            taskNodeId,
            stage);
    }

    private static ProjectStructureTaskCreateRequest ValidateAndNormalize(
        Guid projectId,
        ProjectStructureTaskCreateRequest request)
    {
        if (projectId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "ProjectIdRequired", "A project id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ProjectStructureAgentException(400, "TaskTitleRequired", "A task title is required.");
        }

        var title = request.Title.Trim();
        if (title.Length > MaximumTaskTitleLength)
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskTitleTooLong",
                $"Task title cannot exceed {MaximumTaskTitleLength} characters.");
        }

        if (request.StartUtc == default || request.EndUtc == default)
        {
            throw new ProjectStructureAgentException(400, "TaskDatesRequired", "Task start and end dates are required.");
        }

        var startUtc = request.StartUtc.ToUniversalTime();
        var endUtc = request.EndUtc.ToUniversalTime();
        if (endUtc <= startUtc)
        {
            throw new ProjectStructureAgentException(400, "TaskDateRangeInvalid", "Task end must be later than task start.");
        }

        var durationSeconds = (endUtc - startUtc).TotalSeconds;
        if (durationSeconds > int.MaxValue)
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskDurationTooLong",
                $"Task duration cannot exceed {int.MaxValue} seconds.");
        }

        if (request.Resource is not null)
        {
            ProjectStructureTaskResourceSelectionPolicy.Validate(request.Resource);
        }

        var estimate = NormalizeEstimate(request.Estimate);

        return request with
        {
            Title = title,
            StartUtc = startUtc,
            EndUtc = endUtc,
            AfterTaskNodeId = string.IsNullOrWhiteSpace(request.AfterTaskNodeId)
                ? null
                : request.AfterTaskNodeId.Trim(),
            Estimate = estimate
        };
    }

    private static ProjectTaskEstimate? NormalizeEstimate(ProjectTaskEstimate? estimate)
    {
        if (estimate is null)
        {
            return null;
        }

        try
        {
            return ProjectTaskEstimatePolicy.ValidateAndNormalize(estimate);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or OverflowException)
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskEstimateInvalid",
                exception.Message);
        }
    }
}
