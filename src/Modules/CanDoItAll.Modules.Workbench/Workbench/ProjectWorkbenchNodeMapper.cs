using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectWorkbenchNodeMapper
{
    internal static IReadOnlyList<ProjectStructureNode> MapStructureNodes(
        IReadOnlyList<ProjectObjectRecord> records,
        IReadOnlyList<ProjectObjectLinkRecord> links)
    {
        var projectNodeKeys = records
            .Where(record => ProjectWorkbenchGraphConventions.TryResolveProjectHierarchyNode(record.NodeKey, out _, out _))
            .Select(record => record.NodeKey)
            .ToHashSet(StringComparer.Ordinal);

        return records
            .Select(record =>
            {
                var projectRole = ProjectStructureProjectRole.None;
                Guid? relatedProjectId = null;
                var parentProjectCount = 0;

                if (ProjectWorkbenchGraphConventions.TryResolveProjectHierarchyNode(record.NodeKey, out var nodeKind, out var projectId))
                {
                    relatedProjectId = projectId;
                    projectRole = nodeKind switch
                    {
                        ProjectHierarchyNodeKind.ActiveProject => ProjectStructureProjectRole.ActiveProject,
                        ProjectHierarchyNodeKind.Subproject => ProjectStructureProjectRole.Subproject,
                        ProjectHierarchyNodeKind.RelatedParent when links.Any(link =>
                            string.Equals(link.SourceNodeKey, record.NodeKey, StringComparison.Ordinal) &&
                            ProjectWorkbenchGraphConventions.TryResolveProjectHierarchyNode(link.TargetNodeKey, out var targetKind, out _) &&
                            targetKind == ProjectHierarchyNodeKind.ActiveProject)
                            => ProjectStructureProjectRole.ParentProject,
                        ProjectHierarchyNodeKind.RelatedParent => ProjectStructureProjectRole.AdditionalParentProject,
                        _ => ProjectStructureProjectRole.None
                    };

                    if (projectRole == ProjectStructureProjectRole.Subproject)
                    {
                        parentProjectCount = links.Count(link =>
                            string.Equals(link.TargetNodeKey, record.NodeKey, StringComparison.Ordinal) &&
                            projectNodeKeys.Contains(link.SourceNodeKey));
                    }
                }

                return MapStructureNode(record, projectRole, relatedProjectId, parentProjectCount);
            })
            .ToList();
    }

    internal static ProjectStructureNode MapStructureNode(
        ProjectObjectRecord record,
        ProjectStructureProjectRole projectRole = ProjectStructureProjectRole.None,
        Guid? relatedProjectId = null,
        int parentProjectCount = 0)
    {
        var profile = projectRole switch
        {
            ProjectStructureProjectRole.Subproject => new ProjectObjectVisualProfile("hex", "#1d4ed8", "PR", "Project"),
            ProjectStructureProjectRole.ParentProject => new ProjectObjectVisualProfile("hex", "#334155", "PR", "Parent"),
            ProjectStructureProjectRole.AdditionalParentProject => new ProjectObjectVisualProfile("hex", "#94a3b8", "PR", "Parent"),
            _ => ProjectNodeKindRegistry.ResolveVisualProfile(record.ObjectType, record.ObjectSubtype, record.Status)
        };
        var badges = new List<string>();
        if (record.IsSystemManaged)
        {
            badges.Add("Synced");
        }

        if (record.StartUtc.HasValue)
        {
            badges.Add("Scheduled");
        }

        if (!string.IsNullOrWhiteSpace(record.ObjectSubtype))
        {
            badges.Add(ProjectNodeKindRegistry.ResolveSubtypeBadge(record.ObjectType, record.ObjectSubtype));
        }

        if (!string.IsNullOrWhiteSpace(record.Binding.MediaOriginalFileName))
        {
            badges.Add("Uploaded");
        }

        switch (projectRole)
        {
            case ProjectStructureProjectRole.Subproject:
                badges.Add("Subproject");
                if (parentProjectCount > 1)
                {
                    badges.Add($"{parentProjectCount} parents");
                }

                break;
            case ProjectStructureProjectRole.ParentProject:
                badges.Add("Parent");
                break;
            case ProjectStructureProjectRole.AdditionalParentProject:
                badges.Add("Shared parent");
                break;
        }

        var metadataJson = ProjectNodeLegacyMetadata.SanitizeLegacyReferenceMetadata(record.MetadataJson);
        var markers = ProjectNodeMarkerState.Parse(record.MarkersJson);
        var primaryMarker = ProjectObjectMetadataSerializer.ResolvePrimaryMarker(markers);

        return new ProjectStructureNode(
            record.NodeKey,
            record.ParentNodeKey,
            record.ObjectType,
            record.ObjectSubtype,
            record.Title,
            record.Subtitle,
            record.Status,
            record.Notes,
            record.Binding.Route,
            record.Binding.ExternalArtifactKind,
            record.Binding.ExternalArtifactId,
            record.Binding.MediaRelativePath,
            record.Binding.MediaContentType,
            record.Binding.MediaOriginalFileName,
            record.PositionX,
            record.PositionY,
            profile,
            badges,
            string.IsNullOrWhiteSpace(record.ProgressMode) ? string.Empty : ProjectWorkbenchObjectModeling.NormalizeProgressMode(record.ProgressMode),
            record.ProgressPercent,
            primaryMarker?.Icon ?? string.Empty,
            primaryMarker?.Tone ?? string.Empty,
            primaryMarker?.Label ?? string.Empty,
            markers,
            record.Priority,
            record.StartUtc,
            record.EndUtc,
            metadataJson,
            record.Binding.StorageObjectReferenceJson,
            projectRole,
            relatedProjectId,
            parentProjectCount,
            record.DurationSeconds,
            record.NodeReferences.Clone(),
            record.IsSystemManaged);
    }
}
