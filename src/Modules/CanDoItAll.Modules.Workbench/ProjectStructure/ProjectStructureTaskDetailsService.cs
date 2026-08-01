namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureTaskDetailsService(
    ProjectStructureGanttMutationService ganttMutationService,
    ProjectStructureTaskApplicationService taskApplicationService)
{
    private const string AssignmentSource =
        "project-structure-task-details";

    public async Task<ProjectStructureGanttMutationResult> UpdateAsync(
        Guid projectId,
        ProjectStructureTaskDetailsUpdateRequest request,
        CancellationToken cancellationToken = default)
        => (await UpdateWithPricingAsync(
            projectId,
            request,
            cancellationToken)).Mutation;

    public async Task<ProjectStructureTaskDetailsUpdateResult>
        UpdateWithPricingAsync(
            Guid projectId,
            ProjectStructureTaskDetailsUpdateRequest request,
            CancellationToken cancellationToken = default)
    {
        Validate(projectId, request);
        var expectedState = new ProjectStructureTaskEditState(
            ProjectTaskEstimatePolicy.ValidateAndNormalize(
                request.CurrentEstimate),
            request.CurrentExecution,
            request.CurrentCostBasis,
            request.CurrentDirectAssignmentRevision);
        var applicationRequest =
            new ProjectStructureTaskEditApplicationRequest(
                projectId,
                request.TaskId.Value,
                expectedState,
                request.ProposedEstimate,
                request.ProposedExecution,
                request.AssigneeChanged,
                request.ProposedAssignee,
                AssignmentSource);

        try
        {
            var result = await taskApplicationService.EditAsync(
                applicationRequest,
                (commit, token) =>
                {
                    var mutationRequest =
                        new ProjectStructureTaskDetailsMutationRequest(
                            request.TaskId,
                            request.CurrentTitle,
                            request.ProposedTitle,
                            request.CurrentProgressPercent,
                            request.ProposedProgressPercent,
                            commit.CurrentState.Estimate,
                            commit.ProposedEstimate,
                            request.ScheduleChange,
                            commit.CurrentState.Execution,
                            commit.ProposedExecution,
                            commit.CurrentState.CostBasis,
                            commit.ProposedCostBasis,
                            commit.ProposedCostBasis !=
                                commit.CurrentState.CostBasis,
                            commit.CurrentState
                                .DirectAssignmentRevision);
                    return ganttMutationService.ApplyTaskDetailsAsync(
                        projectId,
                        mutationRequest,
                        token);
                },
                cancellationToken);
            return new ProjectStructureTaskDetailsUpdateResult(
                result.PersistenceResult,
                result.Pricing);
        }
        catch (ProjectStructureTaskApplicationException exception)
        {
            throw new ProjectStructureTaskDetailsException(
                MapErrorCode(exception.Code),
                exception.Message,
                exception);
        }
    }

    private static void Validate(
        Guid projectId,
        ProjectStructureTaskDetailsUpdateRequest request)
    {
        if (projectId == Guid.Empty)
        {
            throw InvalidRequest(
                "A project is required to update task details.");
        }

        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.CurrentTitle) ||
            string.IsNullOrWhiteSpace(request.ProposedTitle))
        {
            throw InvalidRequest("Task titles cannot be empty.");
        }

        if ((request.CurrentProgressPercent !=
                ProjectProgressPolicy.UntrackedPercent &&
             !ProjectProgressPolicy.IsTrackedPercent(
                 request.CurrentProgressPercent)) ||
            !ProjectProgressPolicy.IsTrackedPercent(
                request.ProposedProgressPercent))
        {
            throw InvalidRequest(
                "Current task progress must be untracked (-1) or between 0 and 100 percent; proposed progress must be between 0 and 100 percent.");
        }

        if (request.ScheduleChange is not null &&
            request.ScheduleChange.TaskId != request.TaskId)
        {
            throw InvalidRequest(
                "The schedule change does not belong to the edited task.");
        }

        if (request.CurrentExecution is null ||
            request.ProposedExecution is null)
        {
            throw InvalidRequest(
                "Current and proposed task execution-state snapshots are required.");
        }

        if (request.CurrentDirectAssignmentRevision < 0)
        {
            throw InvalidRequest(
                "A task direct-assignment revision cannot be negative.");
        }

        try
        {
            ProjectTaskEstimatePolicy.ValidateAndNormalize(
                request.CurrentEstimate);
            ProjectTaskEstimatePolicy.ValidateAndNormalize(
                request.ProposedEstimate);
            ProjectTaskExpectedCostBasisPolicy.Validate(
                request.CurrentCostBasis);
            ProjectTaskExecutionStatePolicy.ValidateTransition(
                request.CurrentExecution.State,
                request.ProposedExecution.State);
            ProjectTaskExecutionStatePolicy.Validate(
                request.ProposedExecution.State,
                request.ProposedExecution.ActualStartedAtUtc,
                request.ProposedExecution.ActualEndedAtUtc);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
                ArgumentException or
                OverflowException)
        {
            throw InvalidRequest(exception.Message, exception);
        }

        if (!request.AssigneeChanged ||
            request.ProposedAssignee is null)
        {
            return;
        }

        if (request.ProposedAssignee.Kind is not (
                ProjectStructureTaskResourceKind.Person or
                ProjectStructureTaskResourceKind.Agent) ||
            request.ProposedAssignee.ResourceId == Guid.Empty ||
            request.ProposedAssignee.VersionId.HasValue)
        {
            throw InvalidRequest(
                "Only a person or agent can be assigned directly to a task.");
        }
    }

    private static ProjectStructureTaskDetailsErrorCode MapErrorCode(
        ProjectStructureTaskApplicationErrorCode code)
        => code switch
        {
            ProjectStructureTaskApplicationErrorCode.InvalidRequest =>
                ProjectStructureTaskDetailsErrorCode.InvalidRequest,
            ProjectStructureTaskApplicationErrorCode.ConcurrencyConflict =>
                ProjectStructureTaskDetailsErrorCode.ConcurrencyConflict,
            ProjectStructureTaskApplicationErrorCode.AssignmentConflict =>
                ProjectStructureTaskDetailsErrorCode.AssignmentConflict,
            ProjectStructureTaskApplicationErrorCode.CompensationFailed =>
                ProjectStructureTaskDetailsErrorCode
                    .AssignmentCompensationFailed,
            _ => throw new ArgumentOutOfRangeException(
                nameof(code),
                code,
                "The task application error is not supported.")
        };

    private static ProjectStructureTaskDetailsException InvalidRequest(
        string message,
        Exception? innerException = null)
        => new(
            ProjectStructureTaskDetailsErrorCode.InvalidRequest,
            message,
            innerException);
}
