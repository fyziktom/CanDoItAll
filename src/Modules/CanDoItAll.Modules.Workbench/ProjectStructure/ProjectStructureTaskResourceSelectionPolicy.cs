namespace CanDoItAll.Modules.Workbench;

public static class ProjectStructureTaskResourceSelectionPolicy
{
    internal const string WorkflowInputSettingsResourceKindInvalidErrorCode =
        "TaskWorkflowInputSettingsResourceKindInvalid";
    internal const string DefinitionAttachmentResourceKindInvalidErrorCode =
        "TaskAttachedResourceKindInvalid";

    public static void Validate(ProjectStructureTaskResourceSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (!Enum.IsDefined(selection.Kind))
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskResourceKindInvalid",
                $"Task resource kind '{selection.Kind}' is not supported.");
        }

        if (selection.ResourceId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskResourceRequired",
                "A task resource id is required.");
        }

        if (selection.VersionId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskResourceVersionInvalid",
                "A resource version id cannot be empty.");
        }

        if (selection.Kind != ProjectStructureTaskResourceKind.Workflow &&
            selection.VersionId.HasValue)
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskResourceVersionNotSupported",
                $"Resource kind '{selection.Kind}' does not support a version id.");
        }

        if (selection.Kind == ProjectStructureTaskResourceKind.Workflow &&
            !selection.VersionId.HasValue)
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskWorkflowVersionRequired",
                "A task workflow resource requires an exact workflow version.");
        }
    }

    public static void ValidateDefinitionAttachment(
        ProjectStructureTaskResourceSelection selection)
    {
        Validate(selection);
        if (selection.Kind is ProjectStructureTaskResourceKind.Workflow or
            ProjectStructureTaskResourceKind.Process)
        {
            return;
        }

        throw new ProjectStructureAgentException(
            400,
            DefinitionAttachmentResourceKindInvalidErrorCode,
            "Only a workflow or process can be attached through the typed task-resource path.");
    }

    internal static ProjectStructureWorkflowInputSettings? ValidateAndNormalizeWorkflowInputSettings(
        ProjectStructureTaskResourceSelection selection,
        ProjectStructureWorkflowInputSettings? workflowInputSettings)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (workflowInputSettings is null)
        {
            return null;
        }

        if (selection.Kind != ProjectStructureTaskResourceKind.Workflow)
        {
            throw new ProjectStructureAgentException(
                400,
                WorkflowInputSettingsResourceKindInvalidErrorCode,
                "Workflow input settings can only be supplied when attaching a workflow.");
        }

        return ProjectStructureWorkflowInputSettingsNormalizer.Normalize(workflowInputSettings);
    }
}
