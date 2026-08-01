using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStructureTaskCreateApplicationRequest(
    Guid ProjectId,
    ProjectTaskEstimate Estimate,
    ProjectStructureTaskResourceSelection? Assignee,
    string AssignmentSource);

public sealed record ProjectStructureTaskCreateApplicationResult(
    ProjectStructureNode Task,
    ProjectStructureTaskEstimateRefreshResult Pricing);

public sealed record ProjectStructureTaskEditApplicationRequest(
    Guid ProjectId,
    string TaskNodeId,
    ProjectStructureTaskEditState ExpectedState,
    ProjectTaskEstimate ProposedEstimate,
    ProjectTaskExecutionSnapshot ProposedExecution,
    bool AssigneeChanged,
    ProjectStructureTaskResourceSelection? ProposedAssignee,
    string AssignmentSource);

public sealed record ProjectStructureTaskEditCommitContext(
    ProjectStructureNode CurrentTask,
    ProjectStructureTaskEditState CurrentState,
    ProjectTaskEstimate ProposedEstimate,
    ProjectTaskExecutionSnapshot ProposedExecution,
    ProjectTaskExpectedCostBasis? ProposedCostBasis,
    IReadOnlyList<ProjectPartyAssignmentDetail> DirectAssignments,
    ProjectStructureTaskEstimateRefreshResult Pricing);

public sealed record ProjectStructureTaskEditApplicationResult<TResult>(
    TResult PersistenceResult,
    ProjectStructureTaskEstimateRefreshResult Pricing);

public enum ProjectStructureTaskApplicationErrorCode
{
    InvalidRequest,
    ConcurrencyConflict,
    AssignmentConflict,
    CompensationFailed
}

public sealed class ProjectStructureTaskApplicationException : Exception
{
    public ProjectStructureTaskApplicationException(
        ProjectStructureTaskApplicationErrorCode code,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public ProjectStructureTaskApplicationErrorCode Code { get; }
}

public sealed class ProjectStructureTaskApplicationService(
    ProjectStructureWorkItemAssigneeService assigneeService,
    ProjectStructureTaskEstimateRefreshService estimateRefreshService,
    ProjectStructureTaskEditCompensationService compensationService,
    ProjectWorkbenchService projectWorkbenchService,
    ILogger<ProjectStructureTaskApplicationService> logger)
{
    public async Task<ProjectStructureTaskCreateApplicationResult> CreateAsync(
        ProjectStructureTaskCreateApplicationRequest request,
        Func<
            ProjectStructureTaskEstimateRefreshResult,
            CancellationToken,
            Task<ProjectStructureNode?>> createTask,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);
        ArgumentNullException.ThrowIfNull(createTask);

        var pricing = await estimateRefreshService.RefreshAsync(
            request.ProjectId,
            ProjectTaskExecutionState.NotStarted,
            request.Assignee,
            request.Estimate,
            ProjectStructureTaskMissingResourcePricingPolicy
                .PreserveManualEstimate,
            cancellationToken);
        ProjectStructureNode? createdTask = null;
        try
        {
            createdTask = await createTask(pricing, cancellationToken)
                ?? throw new InvalidOperationException(
                    "Task creation completed without returning the created node.");
            if (request.Assignee is null)
            {
                return new ProjectStructureTaskCreateApplicationResult(
                    createdTask,
                    pricing);
            }

            await assigneeService.ReplaceAsync(
                request.ProjectId,
                createdTask.Id,
                request.Assignee,
                request.AssignmentSource,
                cancellationToken);
            var committed = await assigneeService.ReadAsync(
                request.ProjectId,
                createdTask.Id,
                cancellationToken);
            if (!MatchesSelection(
                    committed.DirectAssignments,
                    request.Assignee))
            {
                throw AssignmentConflict(
                    "The selected assignee was not attached to the created task.");
            }

            return new ProjectStructureTaskCreateApplicationResult(
                committed.Task,
                pricing);
        }
        catch (Exception failure) when (createdTask is not null)
        {
            await CompensateCreateAsync(
                request.ProjectId,
                createdTask.Id,
                failure);
            throw;
        }
    }

    public async Task<ProjectStructureTaskEditApplicationResult<TResult>>
        EditAsync<TResult>(
            ProjectStructureTaskEditApplicationRequest request,
            Func<
                ProjectStructureTaskEditCommitContext,
                CancellationToken,
                Task<TResult>> persist,
            CancellationToken cancellationToken = default)
    {
        ValidateEditRequest(request);
        ArgumentNullException.ThrowIfNull(persist);

        var current = await assigneeService.ReadAsync(
            request.ProjectId,
            request.TaskNodeId,
            cancellationToken);
        var originalState =
            ProjectStructureTaskEditStatePolicy.Read(current.Task);
        if (originalState != request.ExpectedState)
        {
            throw ConcurrencyConflict(
                "The task pricing, execution state, or direct assignments changed before save. Reload the project and try again.");
        }

        var assigneeResolution =
            ProjectStructureTaskAssigneeSelectionPolicy.Resolve(
                current.DirectAssignments,
                request.TaskNodeId);
        var assignmentWasChanged =
            request.AssigneeChanged &&
            request.ProposedAssignee != assigneeResolution.Representative;
        var previousAssignment = assignmentWasChanged
            ? RequireReplaceableAssignment(assigneeResolution)
            : null;

        ProjectTaskExecutionStatePolicy.ValidateTransition(
            originalState.Execution.State,
            request.ProposedExecution.State);
        ProjectTaskExecutionStatePolicy.Validate(
            request.ProposedExecution.State,
            request.ProposedExecution.ActualStartedAtUtc,
            request.ProposedExecution.ActualEndedAtUtc);
        var pricingExecutionState =
            ProjectTaskExecutionStatePolicy.ResolveAuthoritativePricingState(
                originalState.Execution.State,
                request.ProposedExecution.State);
        var submittedEstimate =
            ProjectTaskEstimatePolicy.ValidateAndNormalize(
                request.ProposedEstimate);
        var proposedEstimate =
            ProjectTaskExecutionStatePolicy.AllowsAuthoritativeRepricing(
                pricingExecutionState)
                ? submittedEstimate
                : submittedEstimate with
                {
                    ExpectedCostAmount =
                        originalState.Estimate.ExpectedCostAmount,
                    ExpectedCostCurrencyCode =
                        originalState.Estimate.ExpectedCostCurrencyCode
                };
        var pricingResource =
            ProjectStructureTaskPricingResourcePolicy.Resolve(
                assignmentWasChanged,
                request.ProposedAssignee,
                assigneeResolution,
                originalState.CostBasis);
        var pricing = await estimateRefreshService.RefreshAsync(
            request.ProjectId,
            pricingExecutionState,
            pricingResource,
            proposedEstimate,
            ShouldClearAuthoritativePricing(
                    assignmentWasChanged,
                    request.ProposedAssignee,
                    assigneeResolution,
                    originalState.CostBasis)
                ? ProjectStructureTaskMissingResourcePricingPolicy
                    .ClearAuthoritativeSnapshot
                : ProjectStructureTaskMissingResourcePricingPolicy
                    .PreserveManualEstimate,
            cancellationToken);
        var proposedCostBasis = pricing.ReplacesCostBasis
            ? pricing.CalculatedCostBasis
            : originalState.CostBasis;

        var commitTask = current.Task;
        var commitAssignments = current.DirectAssignments;
        var commitState = originalState;
        var assignmentCommitted = false;
        if (assignmentWasChanged)
        {
            try
            {
                var replacement =
                    await assigneeService.ReplaceIfUnchangedAndReadAsync(
                        request.ProjectId,
                        request.TaskNodeId,
                        request.ProposedAssignee,
                        request.AssignmentSource,
                        current.DirectAssignments,
                        originalState.DirectAssignmentRevision,
                        cancellationToken);
                assignmentCommitted = true;
                commitTask = replacement.Task;
                commitAssignments = replacement.DirectAssignments;
                commitState =
                    ProjectStructureTaskEditStatePolicy.Read(commitTask);
            }
            catch (ProjectStructureAgentException exception)
                when (exception.StatusCode == 409)
            {
                throw AssignmentConflict(exception.Message, exception);
            }
        }

        try
        {
            if (assignmentCommitted &&
                commitState.DirectAssignmentRevision !=
                    checked(originalState.DirectAssignmentRevision + 1))
            {
                throw AssignmentConflict(
                    "The task assignment revision did not advance exactly once. Reload the project and try again.");
            }

            var verified = await assigneeService.ReadAsync(
                request.ProjectId,
                request.TaskNodeId,
                cancellationToken);
            var verifiedState =
                ProjectStructureTaskEditStatePolicy.Read(verified.Task);
            if (verifiedState != commitState ||
                !ProjectStructureTaskAssigneeSelectionPolicy
                    .HasSameDirectAssignments(
                        commitAssignments,
                        verified.DirectAssignments))
            {
                throw ConcurrencyConflict(
                    "The task or its direct assignments changed while authoritative pricing was being prepared. Reload the project and try again.");
            }

            commitTask = verified.Task;
            commitAssignments = verified.DirectAssignments;
            var commitContext = new ProjectStructureTaskEditCommitContext(
                commitTask,
                commitState,
                pricing.Estimate,
                request.ProposedExecution,
                proposedCostBasis,
                commitAssignments,
                pricing);
            var persistenceResult = await persist(
                commitContext,
                cancellationToken);
            return new ProjectStructureTaskEditApplicationResult<TResult>(
                persistenceResult,
                pricing);
        }
        catch (Exception updateFailure) when (assignmentCommitted)
        {
            await CompensateEditAsync(
                request,
                previousAssignment,
                commitAssignments,
                commitState.DirectAssignmentRevision,
                originalState,
                updateFailure);
            throw;
        }
    }

    private async Task CompensateCreateAsync(
        Guid projectId,
        string taskNodeId,
        Exception creationFailure)
    {
        try
        {
            var deletedCount = await projectWorkbenchService.DeleteObjectAsync(
                projectId,
                taskNodeId,
                CancellationToken.None);
            if (deletedCount == 0)
            {
                throw new InvalidOperationException(
                    "The partially created task could not be found during cleanup.");
            }

            logger.LogWarning(
                "Removed a partially created task after its assignee could not be committed. ProjectId={ProjectId} TaskId={TaskId} FailureType={FailureType}",
                Mask(projectId),
                Mask(taskNodeId),
                creationFailure.GetType().Name);
        }
        catch (Exception compensationFailure)
        {
            logger.LogError(
                compensationFailure,
                "Task creation failed and the partially created task could not be removed. ProjectId={ProjectId} TaskId={TaskId} FailureType={FailureType}",
                Mask(projectId),
                Mask(taskNodeId),
                creationFailure.GetType().Name);
            throw new ProjectStructureTaskApplicationException(
                ProjectStructureTaskApplicationErrorCode.CompensationFailed,
                "The task could not be created and its partial record could not be removed. Reload the project before making another change.",
                new AggregateException(
                    creationFailure,
                    compensationFailure));
        }
    }

    private async Task CompensateEditAsync(
        ProjectStructureTaskEditApplicationRequest request,
        ProjectPartyAssignmentDetail? previousAssignment,
        IReadOnlyList<ProjectPartyAssignmentDetail> expectedAssignments,
        long expectedDirectAssignmentRevision,
        ProjectStructureTaskEditState originalState,
        Exception updateFailure)
    {
        try
        {
            var restored =
                await assigneeService.RestoreIfUnchangedAndReadAsync(
                    request.ProjectId,
                    request.TaskNodeId,
                    previousAssignment,
                    expectedAssignments,
                    expectedDirectAssignmentRevision,
                    CancellationToken.None);
            var postRestoreState =
                ProjectStructureTaskEditStatePolicy.Read(restored.Task);
            await compensationService.RestorePricingAsync(
                request.ProjectId,
                request.TaskNodeId,
                postRestoreState,
                originalState,
                CancellationToken.None);
            logger.LogWarning(
                "Restored the previous task assignee and pricing after task persistence failed. ProjectId={ProjectId} TaskId={TaskId} FailureType={FailureType}",
                Mask(request.ProjectId),
                Mask(request.TaskNodeId),
                updateFailure.GetType().Name);
        }
        catch (Exception compensationFailure)
        {
            logger.LogError(
                compensationFailure,
                "Task persistence failed and its previous assignee or pricing could not be restored. ProjectId={ProjectId} TaskId={TaskId} FailureType={FailureType}",
                Mask(request.ProjectId),
                Mask(request.TaskNodeId),
                updateFailure.GetType().Name);
            throw new ProjectStructureTaskApplicationException(
                ProjectStructureTaskApplicationErrorCode.CompensationFailed,
                "The task fields were not saved and its previous assignee or pricing could not be restored. Reload the project before making another change.",
                new AggregateException(
                    updateFailure,
                    compensationFailure));
        }
    }

    private static ProjectPartyAssignmentDetail? RequireReplaceableAssignment(
        ProjectStructureTaskAssigneeSelectionResult resolution)
    {
        if (resolution.DirectAssignments.Count > 1)
        {
            throw AssignmentConflict(
                "This task has multiple direct assignees. Direct Person/Agent changes are read-only so the complete assignment set remains intact.");
        }

        if (resolution.DirectAssignments.Count == 0)
        {
            return null;
        }

        if (resolution.Representative is null)
        {
            throw AssignmentConflict(
                "The task has an unsupported direct assignee type.");
        }

        return resolution.DirectAssignments[0];
    }

    private static bool ShouldClearAuthoritativePricing(
        bool assignmentWasChanged,
        ProjectStructureTaskResourceSelection? proposedAssignee,
        ProjectStructureTaskAssigneeSelectionResult resolution,
        ProjectTaskExpectedCostBasis? costBasis)
        => ProjectStructureTaskPricingResourcePolicy.Resolve(
                assignmentWasChanged,
                proposedAssignee,
                resolution,
                costBasis) is null &&
            (costBasis is not null ||
             assignmentWasChanged &&
             proposedAssignee is null &&
             resolution.Representative is not null);

    private static bool MatchesSelection(
        IReadOnlyList<ProjectPartyAssignmentDetail> assignments,
        ProjectStructureTaskResourceSelection selection)
    {
        var expectedPartyType = selection.Kind switch
        {
            ProjectStructureTaskResourceKind.Person =>
                ProjectPartyType.Person,
            ProjectStructureTaskResourceKind.Agent =>
                ProjectPartyType.AiAgent,
            _ => (ProjectPartyType?)null
        };
        return expectedPartyType.HasValue &&
            assignments.Count == 1 &&
            assignments[0].PartyId == selection.ResourceId &&
            assignments[0].PartyType == expectedPartyType.Value;
    }

    private static void ValidateCreateRequest(
        ProjectStructureTaskCreateApplicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProjectId == Guid.Empty)
        {
            throw InvalidRequest(
                "A project is required to create a task.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.AssignmentSource);
        ProjectTaskEstimatePolicy.ValidateAndNormalize(request.Estimate);
        if (request.Assignee is not null)
        {
            ValidateDirectAssignee(request.Assignee);
        }
    }

    private static void ValidateEditRequest(
        ProjectStructureTaskEditApplicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProjectId == Guid.Empty)
        {
            throw InvalidRequest(
                "A project is required to edit a task.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(request.TaskNodeId);
        ArgumentNullException.ThrowIfNull(request.ExpectedState);
        ArgumentNullException.ThrowIfNull(request.ProposedExecution);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.AssignmentSource);
        if (request.ExpectedState.DirectAssignmentRevision < 0)
        {
            throw InvalidRequest(
                "A task direct-assignment revision cannot be negative.");
        }

        if (request.AssigneeChanged &&
            request.ProposedAssignee is not null)
        {
            ValidateDirectAssignee(request.ProposedAssignee);
        }
    }

    private static void ValidateDirectAssignee(
        ProjectStructureTaskResourceSelection assignee)
    {
        if (assignee.Kind is not (
                ProjectStructureTaskResourceKind.Person or
                ProjectStructureTaskResourceKind.Agent) ||
            assignee.ResourceId == Guid.Empty ||
            assignee.VersionId.HasValue)
        {
            throw InvalidRequest(
                "Only a person or agent can be assigned directly to a task.");
        }
    }

    private static ProjectStructureTaskApplicationException InvalidRequest(
        string message,
        Exception? innerException = null)
        => new(
            ProjectStructureTaskApplicationErrorCode.InvalidRequest,
            message,
            innerException);

    private static ProjectStructureTaskApplicationException
        ConcurrencyConflict(
            string message,
            Exception? innerException = null)
        => new(
            ProjectStructureTaskApplicationErrorCode.ConcurrencyConflict,
            message,
            innerException);

    private static ProjectStructureTaskApplicationException
        AssignmentConflict(
            string message,
            Exception? innerException = null)
        => new(
            ProjectStructureTaskApplicationErrorCode.AssignmentConflict,
            message,
            innerException);

    private static string Mask(Guid value)
    {
        var formatted = value.ToString("N");
        return $"{formatted[..6]}...{formatted[^4..]}";
    }

    private static string Mask(string value)
        => value.Length <= 12
            ? value
            : $"{value[..6]}...{value[^4..]}";
}
