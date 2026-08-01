using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectHierarchyNodeKind
{
    None,
    ActiveProject,
    Subproject,
    RelatedParent
}

internal static class ProjectWorkbenchGraphConventions
{
    internal const string CustomNodePrefix = "custom:";
    internal const string ProjectRootNodePrefix = "project:";
    internal const string ProjectChildNodePrefix = "project-child:";
    internal const string ProjectRelatedParentNodePrefix = "project-related-parent:";

    internal static bool IsCustomNodeKey(string nodeKey)
    {
        return nodeKey.StartsWith(CustomNodePrefix, StringComparison.Ordinal);
    }

    internal static string BuildProjectRootNodeKey(Guid projectId)
    {
        return $"{ProjectRootNodePrefix}{projectId}";
    }

    internal static string NormalizeEditableParentNodeKey(Guid projectId, string? parentNodeKey)
    {
        return string.IsNullOrWhiteSpace(parentNodeKey)
            ? BuildProjectRootNodeKey(projectId)
            : parentNodeKey.Trim();
    }

    internal static ProjectObjectLinkKind ResolveHierarchyLinkKind(Guid projectId, string parentNodeKey)
    {
        return string.Equals(parentNodeKey, BuildProjectRootNodeKey(projectId), StringComparison.Ordinal)
            ? ProjectObjectLinkKind.Contains
            : ProjectObjectLinkKind.BelongsTo;
    }

    internal static bool TryResolveProjectHierarchyNode(
        string nodeKey,
        out ProjectHierarchyNodeKind nodeKind,
        out Guid relatedProjectId)
    {
        nodeKind = ProjectHierarchyNodeKind.None;
        relatedProjectId = Guid.Empty;

        if (nodeKey.StartsWith(ProjectRootNodePrefix, StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(nodeKey[ProjectRootNodePrefix.Length..], out relatedProjectId))
        {
            nodeKind = ProjectHierarchyNodeKind.ActiveProject;
            return true;
        }

        if (nodeKey.StartsWith(ProjectChildNodePrefix, StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(nodeKey[ProjectChildNodePrefix.Length..], out relatedProjectId))
        {
            nodeKind = ProjectHierarchyNodeKind.Subproject;
            return true;
        }

        if (nodeKey.StartsWith(ProjectRelatedParentNodePrefix, StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(nodeKey[ProjectRelatedParentNodePrefix.Length..], out relatedProjectId))
        {
            nodeKind = ProjectHierarchyNodeKind.RelatedParent;
            return true;
        }

        return false;
    }

    internal static (double X, double Y) GetDefaultPosition(ProjectObjectType objectType, int index)
    {
        return objectType switch
        {
            ProjectObjectType.ProjectRoot => (140, 240),
            ProjectObjectType.Phase => (420, 120 + (index * 150)),
            ProjectObjectType.ProjectBlock => (760, 420 + (index * 110)),
            ProjectObjectType.Meeting or ProjectObjectType.Participant or ProjectObjectType.WorkItem => (760, 100 + (index * 120)),
            ProjectObjectType.Repository or ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset or ProjectObjectType.Link or ProjectObjectType.Connector or ProjectObjectType.SecretReference or ProjectObjectType.Script or ProjectObjectType.Environment => (1040, 100 + (index * 120)),
            ProjectObjectType.Recording or ProjectObjectType.Transcript or ProjectObjectType.Infrastructure => (1320, 100 + (index * 120)),
            ProjectObjectType.PromptFlow or ProjectObjectType.PromptSession => (1080, 100 + (index * 150)),
            ProjectObjectType.PromptStep => (1400, 100 + (index * 120)),
            ProjectObjectType.ProcessDefinition => (1560, 160 + (index * 180)),
            ProjectObjectType.ProcessRun => (1880, 180 + (index * 140)),
            ProjectObjectType.ValidationRun => (780, 580 + (index * 120)),
            ProjectObjectType.TestPlan or ProjectObjectType.TestEvidence => (1100, 620 + (index * 140)),
            _ => (420 + ((index % 3) * 220), 820 + ((index / 3) * 140))
        };
    }

    internal static async Task UpsertLinkAsync(
        AppDbContext dbContext,
        Guid projectId,
        string sourceNodeKey,
        string targetNodeKey,
        ProjectObjectLinkKind linkKind,
        bool isSystemManaged,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Set<ProjectObjectLinkRecord>()
            .FirstOrDefaultAsync(item =>
                item.ProjectId == projectId &&
                item.SourceNodeKey == sourceNodeKey &&
                item.TargetNodeKey == targetNodeKey &&
                item.LinkKind == linkKind &&
                item.IsSystemManaged == isSystemManaged,
                cancellationToken);
        if (existing is not null)
        {
            return;
        }

        await dbContext.Set<ProjectObjectLinkRecord>().AddAsync(new ProjectObjectLinkRecord
        {
            ProjectId = projectId,
            SourceNodeKey = sourceNodeKey,
            TargetNodeKey = targetNodeKey,
            LinkKind = linkKind,
            IsSystemManaged = isSystemManaged,
            CreatedAtUtc = DateTimeOffset.UtcNow
        }, cancellationToken);
    }
}
