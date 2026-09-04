using CanDoItAll.Modules.Workbench.ProjectStructure;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureAgentToolResponseProjection
{
    public static IReadOnlyList<string> ExtractWarnings<T>(T response)
    {
        return response switch
        {
            ProjectStructureReadToolData readResponse => readResponse.Warnings,
            ProjectStructureChecklistResponse checklistResponse => checklistResponse.Warnings,
            ProjectStructureDependencyResponse dependencyResponse => dependencyResponse.Warnings,
            ProjectStructureNodesToSubprojectResult nodesToSubprojectResult => nodesToSubprojectResult.Warnings,
            OperationCount operationCount => operationCount.Warnings,
            ProjectStructureImportResult importResult => importResult.Warnings,
            ProjectStructureProcessNodeStartResult processNodeStartResult => processNodeStartResult.Warnings,
            ProjectStructureProcessSubprocessLaunchResult subprocessLaunchResult => subprocessLaunchResult.Warnings,
            ProjectStructureWorkflowNodeCreateResult workflowNodeCreateResult => workflowNodeCreateResult.Warnings,
            ProjectStructureWorkflowAddOptionsResult workflowAddOptionsResult => workflowAddOptionsResult.Warnings,
            ProjectStructureWorkflowNodeStartResult workflowNodeStartResult => workflowNodeStartResult.Warnings,
            ProjectPlanSummary planSummary => planSummary.Warnings,
            _ => []
        };
    }

    public static ProjectStructureCompactNode MapCompactNode(ProjectStructureNodeSummary node)
    {
        return new ProjectStructureCompactNode(
            node.Id,
            node.ParentId,
            node.ObjectType,
            node.ObjectSubtype,
            node.Title,
            node.Subtitle,
            node.Status,
            node.Route,
            node.EffectivePriority,
            node.ProgressMode,
            node.ProgressPercent,
            node.Notes,
            node.MetadataJson,
            node.MediaOriginalFileName,
            node.MediaRelativePath,
            node.MediaContentType,
            node.X,
            node.Y,
            node.DurationSeconds,
            node.ActionCapabilities);
    }
}