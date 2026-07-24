using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureCanonicalTaskCreationPolicy
{
    public static string NormalizeMetadataJson(
        ProjectObjectType objectType,
        string? objectSubtype,
        string metadataJson,
        ProjectObjectTaskPricingInitialization pricingInitialization)
    {
        if (!ProjectStructureCanonicalTaskMutationPolicy.IsTask(
                objectType,
                objectSubtype))
        {
            return metadataJson;
        }

        if (!Enum.IsDefined(pricingInitialization))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pricingInitialization),
                pricingInitialization,
                "Task pricing initialization mode is not defined.");
        }

        var metadata = ProjectObjectMetadataSerializer.Parse(metadataJson);
        metadata.WorkItem ??= new ProjectWorkItemMetadata();
        metadata.WorkItem.WorkItemKind = ProjectWorkItemKind.Task;
        metadata.WorkItem.ExecutionState = ProjectTaskExecutionState.NotStarted;
        metadata.WorkItem.ActualStartedAtUtc = null;
        metadata.WorkItem.ActualEndedAtUtc = null;
        metadata.WorkItem.AssigneePartyDisplayName = string.Empty;

        if (pricingInitialization ==
            ProjectObjectTaskPricingInitialization.ClearAuthoritativePricing)
        {
            if (metadata.WorkItem.ExpectedCostBasis is not null)
            {
                metadata.WorkItem.ExpectedCostAmount = null;
                metadata.WorkItem.ExpectedCostCurrencyCode = string.Empty;
            }

            metadata.WorkItem.ExpectedCostBasis = null;
        }
        else
        {
            ProjectTaskExpectedCostBasisPolicy.Validate(
                metadata.WorkItem.ExpectedCostBasis);
        }

        ProjectObjectMetadataSerializer.Validate(
            ProjectObjectType.WorkItem,
            ProjectObjectSubtypePolicy.Task,
            metadata);
        return ProjectObjectMetadataSerializer.Serialize(metadata);
    }
}
