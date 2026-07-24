using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectStructureTaskPricingCommitPlan(
    Guid ProjectId,
    string TaskNodeId,
    ProjectStructureTaskResourceSelection Resource,
    ProjectTaskExecutionSnapshot ExpectedExecution,
    ProjectTaskEstimate ExpectedEstimate,
    ProjectTaskExpectedCostBasis? ExpectedCostBasis,
    ProjectStructureTaskEstimateRefreshResult Pricing);

public sealed class ProjectStructureTaskPricingCommitService(
    ProjectWorkbenchService projectWorkbenchService,
    ProjectStructureTaskEstimateRefreshService estimateRefreshService,
    ProjectStructureTaskPricingPersistenceService pricingPersistenceService,
    ILogger<ProjectStructureTaskPricingCommitService> logger)
{
    private const string TaskSubtype = "task";

    internal Task<ProjectStructureTaskPricingCommitPlan> PrepareAfterTransitionAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection resource,
        ProjectTaskExecutionSnapshot previousExecution,
        ProjectTaskExecutionSnapshot expectedCurrentExecution,
        CancellationToken cancellationToken = default)
        => PrepareCoreAsync(
            projectId,
            taskNodeId,
            resource,
            previousExecution,
            expectedCurrentExecution,
            cancellationToken);

    private async Task<ProjectStructureTaskPricingCommitPlan> PrepareCoreAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection resource,
        ProjectTaskExecutionSnapshot? previousExecution,
        ProjectTaskExecutionSnapshot? expectedCurrentExecution,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project is required to reprice a task.", nameof(projectId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(taskNodeId);
        ArgumentNullException.ThrowIfNull(resource);

        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var task = surface.Nodes.FirstOrDefault(node =>
            string.Equals(node.Id, taskNodeId, StringComparison.Ordinal));
        if (task is null)
        {
            throw new InvalidOperationException($"Task '{Mask(taskNodeId)}' is no longer available.");
        }

        if (task.IsSystemManaged ||
            task.ObjectType != ProjectObjectType.WorkItem ||
            !string.Equals(task.ObjectSubtype, TaskSubtype, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Node '{Mask(taskNodeId)}' is not an editable canonical task.");
        }

        var metadata = ProjectObjectMetadataSerializer.Parse(task.MetadataJson);
        var workItem = metadata.WorkItem;
        var execution = ReadExecution(workItem);
        if (expectedCurrentExecution is not null &&
            execution != expectedCurrentExecution)
        {
            throw new InvalidOperationException(
                "The task execution state changed before its attached resource could be priced. Reload and retry.");
        }

        var pricingExecution = previousExecution is not null
            ? ProjectTaskExecutionStatePolicy.ResolveAuthoritativePricingState(
                previousExecution.State,
                execution.State)
            : execution.State;
        var currentEstimate = ReadEstimate(workItem);
        var currentCostBasis = workItem?.ExpectedCostBasis;
        var pricing = await estimateRefreshService.RefreshAsync(
            projectId,
            pricingExecution,
            resource,
            currentEstimate,
            ProjectStructureTaskMissingResourcePricingPolicy.PreserveManualEstimate,
            cancellationToken);
        return new ProjectStructureTaskPricingCommitPlan(
            projectId,
            taskNodeId,
            resource,
            execution,
            currentEstimate,
            currentCostBasis,
            pricing);
    }

    internal async Task<ProjectStructureTaskEstimateRefreshResult> CommitAsync(
        ProjectStructureTaskPricingCommitPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var updated = await pricingPersistenceService.CommitAsync(
            plan,
            currentMetadata =>
            {
                currentMetadata.WorkItem ??= new ProjectWorkItemMetadata
                {
                    WorkItemKind = ProjectWorkItemKind.Task
                };
                if (ReadExecution(currentMetadata.WorkItem) != plan.ExpectedExecution ||
                    ReadEstimate(currentMetadata.WorkItem) != plan.ExpectedEstimate ||
                    currentMetadata.WorkItem.ExpectedCostBasis != plan.ExpectedCostBasis)
                {
                    throw new InvalidOperationException(
                        "The task changed while its authoritative price was being calculated. Reload and retry.");
                }

                if (plan.Pricing.Status == ProjectStructureTaskEstimateRefreshStatus.Preserved)
                {
                    return;
                }

                WriteEstimate(currentMetadata.WorkItem, plan.Pricing.Estimate);
                currentMetadata.WorkItem.ExpectedCostBasis = plan.Pricing.CalculatedCostBasis;
            },
            cancellationToken);
        if (updated is null)
        {
            throw new InvalidOperationException(
                $"Task '{Mask(plan.TaskNodeId)}' disappeared while its authoritative price was being committed.");
        }

        logger.LogInformation(
            "Committed authoritative task pricing. ProjectId={ProjectId} TaskId={TaskId} ResourceKind={ResourceKind} PricingStatus={PricingStatus}",
            Mask(plan.ProjectId),
            Mask(plan.TaskNodeId),
            plan.Resource.Kind,
            plan.Pricing.Status);
        return plan.Pricing;
    }

    private static ProjectTaskEstimate ReadEstimate(ProjectWorkItemMetadata? metadata)
        => ProjectTaskEstimatePolicy.ValidateAndNormalize(metadata is null
            ? ProjectTaskEstimate.Empty()
            : new ProjectTaskEstimate(
                metadata.ExpectedEffortHours,
                metadata.ExpectedEffortUnit,
                metadata.ExpectedCostAmount,
                metadata.ExpectedCostCurrencyCode));

    private static ProjectTaskExecutionSnapshot ReadExecution(ProjectWorkItemMetadata? metadata)
        => metadata is null
            ? ProjectTaskExecutionSnapshot.Unknown
            : new ProjectTaskExecutionSnapshot(
                metadata.ExecutionState,
                metadata.ActualStartedAtUtc,
                metadata.ActualEndedAtUtc);

    private static void WriteEstimate(
        ProjectWorkItemMetadata metadata,
        ProjectTaskEstimate estimate)
    {
        metadata.ExpectedEffortHours = estimate.ExpectedEffortHours;
        metadata.ExpectedEffortUnit = estimate.ExpectedEffortUnit;
        metadata.ExpectedCostAmount = estimate.ExpectedCostAmount;
        metadata.ExpectedCostCurrencyCode = estimate.ExpectedCostCurrencyCode;
    }

    private static string Mask(Guid value)
    {
        var formatted = value.ToString("N");
        return $"{formatted[..6]}...{formatted[^4..]}";
    }

    private static string Mask(string value)
        => value.Length <= 12 ? value : $"{value[..6]}...{value[^4..]}";
}
