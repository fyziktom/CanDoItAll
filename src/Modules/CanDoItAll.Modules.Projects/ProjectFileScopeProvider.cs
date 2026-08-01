using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;

namespace CanDoItAll.Modules.Projects;

internal sealed class ProjectFileScopeProvider(ProjectsService projectsService) : IProjectFileScopeProvider
{
    public async ValueTask<ProjectFileScopeSet> ResolveAsync(
        Guid projectId,
        bool includeSubprojects,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            throw ProviderError(FileBrowserErrorCode.InvalidOperation, "The project file scope identifier is invalid.");
        }

        var projects = await projectsService.ListAsync(cancellationToken);
        if (projects.All(project => project.Id != projectId))
        {
            throw ProviderError(FileBrowserErrorCode.NotFound, "The project file scope no longer exists.");
        }

        var hierarchyLinks = await projectsService.ListHierarchyLinksAsync(cancellationToken);
        var projection = ProjectFileFilterProjection.Create(
            projects,
            hierarchyLinks,
            new ProjectFileFilter(
                hierarchyProjectId: projectId,
                hierarchyMode: ProjectHierarchyFilterMode.Descendants,
                includeSubprojects: includeSubprojects));
        FileToolsSemanticScope[] scopes = projection.Projects
            .OrderBy(project => project.Id)
            .Select(project => new FileToolsSemanticScope(
                FileToolsSemanticScopeKind.Project,
                new FileToolsSemanticScopeId(project.Id.ToString("N")),
                project.Name))
            .ToArray();
        return new ProjectFileScopeSet(projectId, scopes, projection.Fingerprint);
    }

    private static FileBrowserProviderException ProviderError(FileBrowserErrorCode code, string message)
        => new(new FileBrowserError(code, message));
}
