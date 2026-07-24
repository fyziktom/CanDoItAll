namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStructureTaskEditState(
    ProjectTaskEstimate Estimate,
    ProjectTaskExecutionSnapshot Execution,
    ProjectTaskExpectedCostBasis? CostBasis,
    long DirectAssignmentRevision);

internal static class ProjectStructureTaskEditStatePolicy
{
    public static ProjectStructureTaskEditState Read(ProjectStructureNode task)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (!ProjectStructureCanonicalTaskMutationPolicy.IsTask(
                task.ObjectType,
                task.ObjectSubtype) ||
            task.IsSystemManaged)
        {
            throw new InvalidOperationException(
                "Task editing requires a canonical editable task.");
        }

        return Read(ProjectObjectMetadataSerializer.Parse(task.MetadataJson));
    }

    public static ProjectStructureTaskEditState Read(
        ProjectObjectMetadataEnvelope metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var workItem = metadata.WorkItem;
        var estimate = ProjectTaskEstimatePolicy.ValidateAndNormalize(
            workItem is null
                ? ProjectTaskEstimate.Empty()
                : new ProjectTaskEstimate(
                    workItem.ExpectedEffortHours,
                    workItem.ExpectedEffortUnit,
                    workItem.ExpectedCostAmount,
                    workItem.ExpectedCostCurrencyCode));
        var execution = workItem is null
            ? ProjectTaskExecutionSnapshot.Unknown
            : new ProjectTaskExecutionSnapshot(
                workItem.ExecutionState,
                workItem.ActualStartedAtUtc,
                workItem.ActualEndedAtUtc);
        ProjectTaskExecutionStatePolicy.Validate(
            execution.State,
            execution.ActualStartedAtUtc,
            execution.ActualEndedAtUtc);
        ProjectTaskExpectedCostBasisPolicy.Validate(
            workItem?.ExpectedCostBasis);
        if (workItem?.DirectAssignmentRevision < 0)
        {
            throw new InvalidOperationException(
                "A task direct-assignment revision cannot be negative.");
        }

        return new ProjectStructureTaskEditState(
            estimate,
            execution,
            workItem?.ExpectedCostBasis,
            workItem?.DirectAssignmentRevision ?? 0);
    }

    public static void WritePricingAndExecution(
        ProjectObjectMetadataEnvelope metadata,
        ProjectTaskEstimate estimate,
        ProjectTaskExecutionSnapshot execution,
        ProjectTaskExpectedCostBasis? costBasis)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(execution);
        var normalizedEstimate =
            ProjectTaskEstimatePolicy.ValidateAndNormalize(estimate);
        ProjectTaskExecutionStatePolicy.Validate(
            execution.State,
            execution.ActualStartedAtUtc,
            execution.ActualEndedAtUtc);
        ProjectTaskExpectedCostBasisPolicy.Validate(costBasis);

        metadata.WorkItem ??= new ProjectWorkItemMetadata
        {
            WorkItemKind = ProjectWorkItemKind.Task
        };
        metadata.WorkItem.WorkItemKind = ProjectWorkItemKind.Task;
        metadata.WorkItem.ExpectedEffortHours =
            normalizedEstimate.ExpectedEffortHours;
        metadata.WorkItem.ExpectedEffortUnit =
            normalizedEstimate.ExpectedEffortUnit;
        metadata.WorkItem.ExpectedCostAmount =
            normalizedEstimate.ExpectedCostAmount;
        metadata.WorkItem.ExpectedCostCurrencyCode =
            normalizedEstimate.ExpectedCostCurrencyCode;
        metadata.WorkItem.ExecutionState = execution.State;
        metadata.WorkItem.ActualStartedAtUtc =
            execution.ActualStartedAtUtc;
        metadata.WorkItem.ActualEndedAtUtc =
            execution.ActualEndedAtUtc;
        metadata.WorkItem.ExpectedCostBasis = costBasis;
    }
}
