using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Resources.Pages;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class ResourceRouteContextSelectionTests
{
    [Fact]
    public void Missing_explicit_project_fails_instead_of_resolving_a_generic_new_resource()
    {
        var result = ResourceRouteContextSelection.Resolve(
            resourceId: null,
            projectId: Guid.NewGuid(),
            resources: [],
            projects: []);

        Assert.False(result.IsResolved);
        Assert.Equal(ResourceRouteContextSelectionStatus.ProjectMissing, result.Status);
    }

    [Fact]
    public void Resource_and_project_must_describe_the_same_canonical_resource()
    {
        var resourceId = Guid.NewGuid();
        var resourceProjectId = Guid.NewGuid();
        var requestedProjectId = Guid.NewGuid();
        var resource = CreateResource(resourceId, resourceProjectId);

        var result = ResourceRouteContextSelection.Resolve(
            resourceId,
            requestedProjectId,
            [resource],
            [CreateProject(resourceProjectId), CreateProject(requestedProjectId)]);

        Assert.False(result.IsResolved);
        Assert.Equal(ResourceRouteContextSelectionStatus.ResourceProjectMismatch, result.Status);
        Assert.Same(resource, result.Resource);
    }

    [Fact]
    public void Matching_explicit_resource_and_project_resolve_exactly()
    {
        var resourceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var resource = CreateResource(resourceId, projectId);

        var result = ResourceRouteContextSelection.Resolve(
            resourceId,
            projectId,
            [resource],
            [CreateProject(projectId)]);

        Assert.True(result.IsResolved);
        Assert.Equal(ResourceRouteContextSelectionStatus.Resolved, result.Status);
        Assert.Same(resource, result.Resource);
    }

    private static ResourceSummary CreateResource(Guid resourceId, Guid projectId)
        => new(
            resourceId,
            projectId,
            "Project",
            LegacyResourceKind: null,
            ResourceConnectorPluginKeys.Repository,
            "Repository",
            "Resource",
            "resource://example",
            ResourceValidationStatus.Valid,
            ResourceSensitivity.Normal);

    private static ProjectSummary CreateProject(Guid projectId)
        => new(
            projectId,
            "Project",
            ProjectStatus.Active,
            "Delivery",
            1,
            0,
            0,
            DateTimeOffset.UtcNow);
}
