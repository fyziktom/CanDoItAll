using CanDoItAll.AppComponents.FileTools;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureFileActionCoordinator(
    IProjectFileScopeProvider projectScopeProvider,
    IProjectStructureNodeFileScopeProvider nodeScopeResolver,
    IFileToolsBrowseSessionFactory browseSessionFactory,
    IFileToolsBrowseItemActivator itemActivator,
    IFileToolsBrowseItemActionService itemActionService,
    IFileToolsKnownFileSessionFactory knownFileSessionFactory,
    IFileToolsKnownFileSessionReleaser knownFileSessionReleaser,
    ILogger<ProjectStructureFileActionCoordinator> logger)
{
    private const int MaximumSources = ProjectFileScopeSet.MaximumScopes;
    private static readonly FileBrowserSortDescriptor DefaultSort = new(
        FileBrowserSortField.ProviderNative,
        FileBrowserSortDirection.Ascending,
        FoldersFirst: false);
    private static readonly FileBrowserSearchBudget SearchBudget = new(
        maximumContainers: 32,
        maximumItems: 2_000,
        maximumDuration: TimeSpan.FromSeconds(5),
        maximumConcurrentRequests: 1,
        maximumMatches: 200,
        maximumRetainedBytes: 2L * 1024 * 1024);

    public ProjectStructureFileCollectionRequest CreateRequest(
        Guid currentProjectId,
        string currentProjectName,
        ProjectStructureNode? node)
    {
        if (currentProjectId == Guid.Empty)
        {
            throw new ArgumentException("A current project identifier is required.", nameof(currentProjectId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(currentProjectName);
        if (node is null)
        {
            return new ProjectStructureProjectFileCollectionRequest(currentProjectId, currentProjectName.Trim());
        }

        if (node.ProjectRole != ProjectStructureProjectRole.None)
        {
            Guid targetProjectId = node.ProjectRole == ProjectStructureProjectRole.ActiveProject
                ? currentProjectId
                : node.RelatedProjectId ?? Guid.Empty;
            if (targetProjectId == Guid.Empty)
            {
                throw ProviderError(FileBrowserErrorCode.InvalidOperation, "The related project file target is invalid.");
            }

            return new ProjectStructureProjectFileCollectionRequest(targetProjectId, node.Title);
        }

        if (!ProjectStructureFileActions.CanBrowseFiles(node))
        {
            throw ProviderError(FileBrowserErrorCode.Unsupported, "The selected node does not expose a file collection.");
        }

        return new ProjectStructureNodeFileCollectionRequest(currentProjectId, node.Id, node.Title);
    }

    public async ValueTask<ProjectStructureFileBrowserWorkspace> OpenAsync(
        ProjectStructureFileCollectionRequest request,
        bool includeSubprojects,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ScopeResolution scopeResolution = await ResolveScopesAsync(
            request,
            includeSubprojects,
            cancellationToken);
        var providers = new List<IFileBrowserProvider>(scopeResolution.Scopes.Count);
        var sourceScopes = new Dictionary<FileBrowserSourceId, FileToolsSemanticScope>();
        var sourceActions = new Dictionary<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability>();
        var revisionParts = new List<string>(scopeResolution.Scopes.Count);
        FileBrowserSortDescriptor? defaultSort = null;
        foreach (FileToolsSemanticScope scope in scopeResolution.Scopes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileToolsBrowseSession session = await browseSessionFactory.CreateAsync(scope, cancellationToken);
            defaultSort ??= session.DefaultSort;
            if (session.DefaultSort != defaultSort)
            {
                throw ProviderError(
                    FileBrowserErrorCode.CorruptProviderResponse,
                    "Project Structure file sources disagree on their default ordering contract.");
            }

            string[] sourceIds = session.Providers
                .Select(provider => provider.Descriptor.Id.Value)
                .OrderBy(sourceId => sourceId, StringComparer.Ordinal)
                .ToArray();
            revisionParts.Add(string.Join(':', scope.Id.Value, session.Revision.Value, string.Join(',', sourceIds)));
            foreach (IFileBrowserProvider provider in session.Providers)
            {
                if (providers.Count >= MaximumSources)
                {
                    throw ProviderError(
                        FileBrowserErrorCode.InvalidOperation,
                        $"Project Structure file browsing supports at most {MaximumSources} sources.");
                }

                if (!sourceScopes.TryAdd(provider.Descriptor.Id, scope))
                {
                    throw ProviderError(
                        FileBrowserErrorCode.CorruptProviderResponse,
                        "Project Structure file browsing produced a duplicate source identifier.");
                }

                sourceActions.Add(
                    provider.Descriptor.Id,
                    FileToolsHostActionCapabilityCapture.Resolve(provider));

                providers.Add(provider);
            }
        }

        if (providers.Count == 0)
        {
            throw ProviderError(FileBrowserErrorCode.NotFound, "The selected file collection has no available sources.");
        }

        string revision = BuildRevision(scopeResolution.Fingerprint, revisionParts);
        var sourceSet = new FileBrowserSourceSet(revision, providers);
        var options = new FileBrowserSessionOptions(
            pageSize: 50,
            defaultSort: defaultSort ?? DefaultSort,
            retentionMode: FileBrowserStateRetentionMode.Disabled,
            searchBudget: SearchBudget);
        var browser = new FileBrowserSession(sourceSet, options: options);
        logger.LogInformation(
            "Project Structure file collection opened. Target={TargetKind} ScopeCount={ScopeCount} SourceCount={SourceCount} IncludeSubprojects={IncludeSubprojects} Revision={Revision}.",
            request is ProjectStructureProjectFileCollectionRequest ? "project" : "node",
            scopeResolution.Scopes.Count,
            sourceScopes.Count,
            includeSubprojects,
            revision[..12]);
        return new ProjectStructureFileBrowserWorkspace(
            request,
            browser,
            sourceScopes,
            sourceActions,
            revision,
            includeSubprojects);
    }

    public async ValueTask<ProjectStructureKnownFileInteraction> ActivateAsync(
        ProjectStructureFileBrowserWorkspace workspace,
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (workspace.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(workspace));
        }

        FileToolsSemanticScope scope = ResolveScope(workspace, itemKey.SourceId);

        FileToolsKnownFileActivation activation = await itemActivator.ActivateAsync(
            scope,
            itemKey,
            FileToolsKnownFileIntent.ReadOnly,
            cancellationToken);
        try
        {
            FileToolsKnownFileSession session = await knownFileSessionFactory.CreateAsync(
                activation.Request,
                cancellationToken);
            var request = new FileInteractionRequest(
                session.File,
                activation.FileName,
                FileInteractionMode.View,
                activation.MediaType,
                activation.Size);
            return new ProjectStructureKnownFileInteraction(request, session, knownFileSessionReleaser);
        }
        catch
        {
            await knownFileSessionReleaser.ReleaseAsync(activation.Request.File, CancellationToken.None);
            throw;
        }
    }

    public ValueTask<FileToolsBrowseItemActionResult> LaunchAsync(
        ProjectStructureFileBrowserWorkspace workspace,
        FileBrowserItemKey itemKey,
        FileToolsLocalFileAction action,
        CancellationToken cancellationToken = default)
    {
        FileToolsSemanticScope scope = ResolveScope(workspace, itemKey.SourceId);
        return itemActionService.LaunchAsync(scope, itemKey, action, cancellationToken);
    }

    public ValueTask<IFileToolsDownloadLease> AuthorizeDownloadAsync(
        ProjectStructureFileBrowserWorkspace workspace,
        FileBrowserItemKey itemKey,
        CancellationToken cancellationToken = default)
    {
        FileToolsSemanticScope scope = ResolveScope(workspace, itemKey.SourceId);
        return itemActionService.AuthorizeDownloadAsync(scope, itemKey, cancellationToken);
    }

    private static FileToolsSemanticScope ResolveScope(
        ProjectStructureFileBrowserWorkspace workspace,
        FileBrowserSourceId sourceId)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (workspace.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(workspace));
        }

        if (!workspace.TryGetScope(sourceId, out FileToolsSemanticScope? scope) || scope is null)
        {
            throw ProviderError(
                FileBrowserErrorCode.Conflict,
                "The selected file source is no longer part of the Project Structure collection.");
        }

        return scope;
    }

    private async ValueTask<ScopeResolution> ResolveScopesAsync(
        ProjectStructureFileCollectionRequest request,
        bool includeSubprojects,
        CancellationToken cancellationToken)
    {
        switch (request)
        {
            case ProjectStructureProjectFileCollectionRequest projectRequest:
                ProjectFileScopeSet projectScopes = await projectScopeProvider.ResolveAsync(
                    projectRequest.ProjectId,
                    includeSubprojects,
                    cancellationToken);
                return new ScopeResolution(projectScopes.Scopes, projectScopes.Fingerprint);
            case ProjectStructureNodeFileCollectionRequest nodeRequest:
                FileToolsSemanticScope nodeScope = await nodeScopeResolver.ResolveNodeCollectionAsync(
                    nodeRequest.ProjectId,
                    nodeRequest.NodeId,
                    cancellationToken);
                return new ScopeResolution(
                    [nodeScope],
                    Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(nodeScope.Id.Value))));
            default:
                throw new ArgumentOutOfRangeException(nameof(request));
        }
    }

    private static string BuildRevision(string fingerprint, IReadOnlyList<string> revisionParts)
    {
        string canonical = string.Join('\n',
            [
                "project-structure-files-v1",
                fingerprint,
                .. revisionParts
            ]);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static FileBrowserProviderException ProviderError(FileBrowserErrorCode code, string message)
        => new(new FileBrowserError(code, message));

    private sealed record ScopeResolution(
        IReadOnlyList<FileToolsSemanticScope> Scopes,
        string Fingerprint);
}

internal sealed class ProjectStructureFileBrowserWorkspace : IAsyncDisposable
{
    private IReadOnlyDictionary<FileBrowserSourceId, FileToolsSemanticScope> sourceScopes;
    private IReadOnlyDictionary<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability> sourceActions;
    private bool disposed;

    public ProjectStructureFileBrowserWorkspace(
        ProjectStructureFileCollectionRequest request,
        IFileBrowserSession browser,
        IReadOnlyDictionary<FileBrowserSourceId, FileToolsSemanticScope> sourceScopes,
        IReadOnlyDictionary<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability> sourceActions,
        string revision,
        bool includeSubprojects)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Browser = browser ?? throw new ArgumentNullException(nameof(browser));
        this.sourceScopes = sourceScopes ?? throw new ArgumentNullException(nameof(sourceScopes));
        this.sourceActions = sourceActions ?? throw new ArgumentNullException(nameof(sourceActions));
        if (!sourceScopes.Keys.ToHashSet().SetEquals(sourceActions.Keys))
        {
            throw new ArgumentException("Every file source must declare host action availability.", nameof(sourceActions));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        Revision = revision;
        IncludeSubprojects = includeSubprojects;
    }

    public ProjectStructureFileCollectionRequest Request { get; }

    public IFileBrowserSession Browser { get; }

    public string Revision { get; }

    public bool IncludeSubprojects { get; }

    public int SourceCount => sourceScopes.Count;

    public bool IsDisposed => disposed;

    public bool TryGetScope(FileBrowserSourceId sourceId, out FileToolsSemanticScope? scope)
        => sourceScopes.TryGetValue(sourceId, out scope);

    public bool SupportsLocalOpen(FileBrowserSourceId sourceId)
        => sourceActions.TryGetValue(sourceId, out FileToolsBrowseSourceActionAvailability actions) &&
           actions.SupportsLocalOpen;

    public bool SupportsDownload(FileBrowserSourceId sourceId)
        => sourceActions.TryGetValue(sourceId, out FileToolsBrowseSourceActionAvailability actions) &&
           actions.SupportsDownload;

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        sourceScopes = new Dictionary<FileBrowserSourceId, FileToolsSemanticScope>();
        sourceActions = new Dictionary<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability>();
        await Browser.DisposeAsync();
    }
}
