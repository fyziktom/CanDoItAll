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
        var lifetimeId = Guid.NewGuid();
        var resource = CreateResource(resourceId, projectId) with { ProjectLifetimeId = lifetimeId };

        var result = ResourceRouteContextSelection.Resolve(
            resourceId,
            projectId,
            [resource],
            [CreateProject(projectId, lifetimeId)]);

        Assert.True(result.IsResolved);
        Assert.Equal(ResourceRouteContextSelectionStatus.Resolved, result.Status);
        Assert.Same(resource, result.Resource);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retained_or_unbound_resource_does_not_resolve_as_the_recreated_projects_context(bool unbound) {
        var projectId = Guid.NewGuid();
        var resource = CreateResource(Guid.NewGuid(), projectId) with {
            ProjectLifetimeId = unbound ? null : Guid.NewGuid()
        };
        var current = CreateProject(projectId);

        var scoped = ResourceRouteContextSelection.Resolve(resource.Id, projectId, [resource], [current]);
        var historical = ResourceRouteContextSelection.Resolve(resource.Id, null, [resource], [current]);

        Assert.False(scoped.IsResolved);
        Assert.Equal(ResourceRouteContextSelectionStatus.ResourceProjectMismatch, scoped.Status);
        Assert.True(historical.IsResolved);
        Assert.Same(resource, historical.Resource);
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

    private static ProjectWriteSelection CreateProject(Guid projectId, Guid? lifetimeId = null)
        => new(projectId, "Project", new(Guid.NewGuid(), projectId, lifetimeId ?? Guid.NewGuid()));
}
