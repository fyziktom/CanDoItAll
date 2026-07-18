using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Resources.Pages;

internal enum ResourceRouteContextSelectionStatus
{
    Resolved,
    ResourceMissing,
    ProjectMissing,
    ResourceProjectMismatch
}

internal sealed record ResourceRouteContextSelection(
    ResourceRouteContextSelectionStatus Status,
    ResourceSummary? Resource)
{
    public bool IsResolved => Status == ResourceRouteContextSelectionStatus.Resolved;

    public static ResourceRouteContextSelection Resolve(
        Guid? resourceId,
        Guid? projectId,
        IReadOnlyList<ResourceSummary> resources,
        IReadOnlyList<ProjectSummary> projects)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(projects);

        var resource = resourceId.HasValue
            ? resources.FirstOrDefault(candidate => candidate.Id == resourceId.Value)
            : null;
        if (resourceId.HasValue && resource is null)
        {
            return new ResourceRouteContextSelection(
                ResourceRouteContextSelectionStatus.ResourceMissing,
                null);
        }

        if (projectId.HasValue && projects.All(candidate => candidate.Id != projectId.Value))
        {
            return new ResourceRouteContextSelection(
                ResourceRouteContextSelectionStatus.ProjectMissing,
                resource);
        }

        if (resource is not null &&
            projectId.HasValue &&
            resource.ProjectId != projectId.Value)
        {
            return new ResourceRouteContextSelection(
                ResourceRouteContextSelectionStatus.ResourceProjectMismatch,
                resource);
        }

        return new ResourceRouteContextSelection(
            ResourceRouteContextSelectionStatus.Resolved,
            resource);
    }
}
