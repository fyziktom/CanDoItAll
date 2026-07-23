using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureCanvasTaskCommitPolicy
{
    private const string TaskSubtype = "task";

    public static ProjectStructureTaskEditState Read(
        ProjectStructureNode taskNode)
        => ProjectStructureTaskEditStatePolicy.Read(taskNode);

    public static void ValidateCurrentMetadata(
        ProjectObjectMetadataEnvelope currentMetadata,
        ProjectStructureTaskEditState expectedSnapshot)
    {
        ArgumentNullException.ThrowIfNull(currentMetadata);
        ArgumentNullException.ThrowIfNull(expectedSnapshot);

        if (ProjectStructureTaskEditStatePolicy.Read(currentMetadata) !=
            expectedSnapshot)
        {
            throw new InvalidOperationException(
                "The task pricing or execution state changed before save. Reload the project and try again.");
        }
    }

    public static ProjectObjectCreateRequest ApplyCreate(
        ProjectObjectCreateRequest request,
        ProjectStructureTaskEstimateRefreshResult pricing)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(pricing);
        if (request.ObjectType != ProjectObjectType.WorkItem ||
            !string.Equals(request.ObjectSubtype, TaskSubtype, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Canvas task creation requires a canonical task request.");
        }

        var metadata = ProjectObjectMetadataSerializer.Parse(request.MetadataJson);
        metadata.WorkItem ??= new ProjectWorkItemMetadata
        {
            WorkItemKind = ProjectWorkItemKind.Task
        };
        metadata.WorkItem.WorkItemKind = ProjectWorkItemKind.Task;
        WriteEstimate(metadata.WorkItem, pricing.Estimate);
        WriteExecution(metadata.WorkItem, ProjectTaskExecutionSnapshot.NotStarted);
        metadata.WorkItem.ExpectedCostBasis = pricing.CalculatedCostBasis;
        ProjectTaskExpectedCostBasisPolicy.Validate(metadata.WorkItem.ExpectedCostBasis);
        ProjectObjectMetadataSerializer.Validate(
            ProjectObjectType.WorkItem,
            TaskSubtype,
            metadata);
        return request with
        {
            MetadataJson = ProjectObjectMetadataSerializer.Serialize(metadata),
            TaskPricingInitialization =
                ProjectObjectTaskPricingInitialization.PreserveValidatedAuthoritativePricing
        };
    }

    public static ProjectObjectEditRequest ApplyEdit(
        ProjectStructureNode taskNode,
        ProjectObjectEditRequest request,
        ProjectTaskExecutionSnapshot execution,
        ProjectStructureTaskEstimateRefreshResult pricing,
        ProjectTaskExpectedCostBasis? costBasis)
    {
        ArgumentNullException.ThrowIfNull(taskNode);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(pricing);
        EnsureCanonicalTask(taskNode);
        ProjectTaskExecutionStatePolicy.Validate(
            execution.State,
            execution.ActualStartedAtUtc,
            execution.ActualEndedAtUtc);
        ProjectTaskExpectedCostBasisPolicy.Validate(costBasis);

        var metadata = ProjectObjectMetadataSerializer.Parse(request.MetadataJson);
        metadata.WorkItem ??= new ProjectWorkItemMetadata
        {
            WorkItemKind = ProjectWorkItemKind.Task
        };
        metadata.WorkItem.WorkItemKind = ProjectWorkItemKind.Task;
        WriteEstimate(metadata.WorkItem, pricing.Estimate);
        WriteExecution(metadata.WorkItem, execution);
        metadata.WorkItem.ExpectedCostBasis = costBasis;
        ProjectObjectMetadataSerializer.Validate(
            ProjectObjectType.WorkItem,
            TaskSubtype,
            metadata);
        return request with
        {
            MetadataJson = ProjectObjectMetadataSerializer.Serialize(metadata)
        };
    }

    private static void EnsureCanonicalTask(ProjectStructureNode taskNode)
    {
        if (taskNode.IsSystemManaged ||
            taskNode.ObjectType != ProjectObjectType.WorkItem ||
            !string.Equals(taskNode.ObjectSubtype, TaskSubtype, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Canvas task editing requires a canonical task node.");
        }
    }

    private static void WriteEstimate(
        ProjectWorkItemMetadata workItem,
        ProjectTaskEstimate estimate)
    {
        var normalized = ProjectTaskEstimatePolicy.ValidateAndNormalize(estimate);
        workItem.ExpectedEffortHours = normalized.ExpectedEffortHours;
        workItem.ExpectedEffortUnit = normalized.ExpectedEffortUnit;
        workItem.ExpectedCostAmount = normalized.ExpectedCostAmount;
        workItem.ExpectedCostCurrencyCode = normalized.ExpectedCostCurrencyCode;
    }

    private static void WriteExecution(
        ProjectWorkItemMetadata workItem,
        ProjectTaskExecutionSnapshot execution)
    {
        ProjectTaskExecutionStatePolicy.Validate(
            execution.State,
            execution.ActualStartedAtUtc,
            execution.ActualEndedAtUtc);
        workItem.ExecutionState = execution.State;
        workItem.ActualStartedAtUtc = execution.ActualStartedAtUtc;
        workItem.ActualEndedAtUtc = execution.ActualEndedAtUtc;
    }
}
