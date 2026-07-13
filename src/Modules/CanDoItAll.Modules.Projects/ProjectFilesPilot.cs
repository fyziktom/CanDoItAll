using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;

namespace CanDoItAll.Modules.Projects;

public interface IProjectFilesPilotCoordinator
{
    ValueTask<ProjectFilesPilotWorkspace> OpenAsync(
        Guid projectId,
        string projectName,
        CancellationToken cancellationToken = default);

    ValueTask<ProjectFilesPilotInteraction> ActivateAsync(
        ProjectFilesPilotWorkspace workspace,
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken = default);
}

internal sealed class ProjectFilesPilotCoordinator(
    IFileToolsBrowseSessionFactory browseSessionFactory,
    ProjectFileReadOnlyInteractionFactory interactionFactory) : IProjectFilesPilotCoordinator
{
    public async ValueTask<ProjectFilesPilotWorkspace> OpenAsync(
        Guid projectId,
        string projectName,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project identifier is required.", nameof(projectId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.Project,
            new FileToolsSemanticScopeId(projectId.ToString("N")),
            projectName);
        FileToolsBrowseSession hostSession = await browseSessionFactory.CreateAsync(scope, cancellationToken);
        FileBrowserSession browser = ProjectFileBrowserPolicy.Create(
            hostSession.Providers,
            hostSession.DefaultSort);
        return new ProjectFilesPilotWorkspace(projectId, projectName, scope, browser);
    }

    public async ValueTask<ProjectFilesPilotInteraction> ActivateAsync(
        ProjectFilesPilotWorkspace workspace,
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (workspace.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(workspace));
        }

        return await interactionFactory.CreateAsync(
            workspace.Scope,
            itemKey,
            cancellationToken);
    }
}
