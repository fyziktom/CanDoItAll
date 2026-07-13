using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Modules.Projects;

public interface IProjectFilePortfolioCoordinator
{
    ValueTask<ProjectFilePortfolioWorkspace> OpenAsync(
        ProjectFileFilterProjection projection,
        CancellationToken cancellationToken = default);

    ValueTask<bool> UpdateAsync(
        ProjectFilePortfolioWorkspace workspace,
        ProjectFileFilterProjection projection,
        CancellationToken cancellationToken = default);

    ValueTask<ProjectFilesPilotInteraction> ActivateAsync(
        ProjectFilePortfolioWorkspace workspace,
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken = default);
}

internal sealed class ProjectFilePortfolioCoordinator(
    IFileToolsBrowseSessionFactory browseSessionFactory,
    ProjectFileReadOnlyInteractionFactory interactionFactory,
    ILogger<ProjectFilePortfolioCoordinator> logger) : IProjectFilePortfolioCoordinator
{
    private static readonly FileBrowserSortDescriptor DefaultSort = new(
        FileBrowserSortField.ProviderNative,
        FileBrowserSortDirection.Ascending,
        FoldersFirst: false);

    public async ValueTask<ProjectFilePortfolioWorkspace> OpenAsync(
        ProjectFileFilterProjection projection,
        CancellationToken cancellationToken = default)
    {
        ProjectFilePortfolioSourceSet resolved = await ResolveAsync(projection, cancellationToken);
        FileBrowserSession browser = ProjectFileBrowserPolicy.Create(resolved.Sources, resolved.DefaultSort);
        var workspace = new ProjectFilePortfolioWorkspace(browser, resolved);
        LogSourceSet("opened", resolved);
        return workspace;
    }

    public async ValueTask<bool> UpdateAsync(
        ProjectFilePortfolioWorkspace workspace,
        ProjectFileFilterProjection projection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (workspace.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(workspace));
        }

        ProjectFilePortfolioSourceSet resolved = await ResolveAsync(projection, cancellationToken);
        if (resolved.Revision == workspace.Revision)
        {
            return false;
        }

        await workspace.ReplaceSourcesAsync(resolved, cancellationToken);
        LogSourceSet("updated", resolved);
        return true;
    }

    public ValueTask<ProjectFilesPilotInteraction> ActivateAsync(
        ProjectFilePortfolioWorkspace workspace,
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (workspace.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(workspace));
        }

        if (!workspace.TryGetScope(itemKey.SourceId, out FileToolsSemanticScope? scope) || scope is null)
        {
            throw ProviderError(
                FileBrowserErrorCode.Conflict,
                "The selected project file source is no longer part of the filtered portfolio.");
        }

        return interactionFactory.CreateAsync(scope, itemKey, cancellationToken);
    }

    private async ValueTask<ProjectFilePortfolioSourceSet> ResolveAsync(
        ProjectFileFilterProjection projection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (projection.Projects.Count > ProjectFileBrowserPolicy.MaximumProjectSources)
        {
            throw ProviderError(
                FileBrowserErrorCode.InvalidOperation,
                $"Narrow the project filters to {ProjectFileBrowserPolicy.MaximumProjectSources} projects or fewer before browsing files.");
        }

        var providers = new List<IFileBrowserProvider>(projection.Projects.Count);
        var scopes = new Dictionary<FileBrowserSourceId, FileToolsSemanticScope>();
        var revisionParts = new List<string>(projection.Projects.Count);
        FileBrowserSortDescriptor? defaultSort = null;
        foreach (ProjectSummary project in projection.Projects.OrderBy(project => project.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scope = new FileToolsSemanticScope(
                FileToolsSemanticScopeKind.Project,
                new FileToolsSemanticScopeId(project.Id.ToString("N")),
                project.Name);
            FileToolsBrowseSession session = await browseSessionFactory.CreateAsync(scope, cancellationToken);
            defaultSort ??= session.DefaultSort;
            if (session.DefaultSort != defaultSort)
            {
                throw ProviderError(
                    FileBrowserErrorCode.CorruptProviderResponse,
                    "Project file sources disagree on their default ordering contract.");
            }

            string[] sourceIds = session.Providers
                .Select(provider => provider.Descriptor.Id.Value)
                .OrderBy(sourceId => sourceId, StringComparer.Ordinal)
                .ToArray();
            revisionParts.Add(string.Join(':',
                project.Id.ToString("N"),
                session.Revision.Value,
                string.Join(',', sourceIds)));
            foreach (IFileBrowserProvider provider in session.Providers)
            {
                if (providers.Count >= ProjectFileBrowserPolicy.MaximumProjectSources)
                {
                    throw ProviderError(
                        FileBrowserErrorCode.InvalidOperation,
                        "The filtered project portfolio resolves to too many file sources.");
                }

                if (!scopes.TryAdd(provider.Descriptor.Id, scope))
                {
                    throw ProviderError(
                        FileBrowserErrorCode.CorruptProviderResponse,
                        "The filtered project portfolio produced a duplicate file source identifier.");
                }

                providers.Add(provider);
            }
        }

        var revision = BuildRevision(projection.Fingerprint, revisionParts);
        return new ProjectFilePortfolioSourceSet(
            new FileBrowserSourceSet(revision.Value, providers),
            scopes,
            revision,
            projection.Projects.Count,
            defaultSort ?? DefaultSort);
    }

    private static ProjectFilePortfolioRevision BuildRevision(
        string projectionFingerprint,
        IReadOnlyList<string> revisionParts)
    {
        string canonical = string.Join('\n',
            [
                "project-file-portfolio-v1",
                projectionFingerprint,
                .. revisionParts
            ]);
        return new ProjectFilePortfolioRevision(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))));
    }

    private void LogSourceSet(string action, ProjectFilePortfolioSourceSet sources)
    {
        logger.LogInformation(
            "Project file portfolio {Action}. Projects={ProjectCount} Sources={SourceCount} Revision={Revision}.",
            action,
            sources.ProjectCount,
            sources.SourceScopes.Count,
            sources.Revision.Value[..12]);
    }

    private static FileBrowserProviderException ProviderError(FileBrowserErrorCode code, string message)
        => new(new FileBrowserError(code, message));
}
