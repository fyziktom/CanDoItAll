using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AppComponents.FileTools;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRunFilesCoordinator(
    IProcessRunFileScopeProvider scopeProvider,
    IFileToolsBrowseSessionFactory browseSessionFactory,
    IFileToolsBrowseItemActivator itemActivator,
    IFileToolsKnownFileSessionFactory knownFileSessionFactory,
    IFileToolsKnownFileSessionReleaser knownFileSessionReleaser,
    ILogger<ProcessRunFilesCoordinator> logger)
{
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

    public async ValueTask<ProcessRunFileWorkspace> OpenAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        ProcessRunFileScopeSet scopeSet = await scopeProvider.ResolveAsync(runId, cancellationToken);
        var providers = new List<IFileBrowserProvider>(scopeSet.Scopes.Count);
        var sourceScopes = new Dictionary<FileBrowserSourceId, FileToolsSemanticScope>();
        var sourceActions = new Dictionary<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability>();
        var revisionParts = new List<string>(scopeSet.Scopes.Count);
        FileBrowserSortDescriptor? defaultSort = null;
        foreach (FileToolsSemanticScope scope in scopeSet.Scopes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileToolsBrowseSession session = await browseSessionFactory.CreateAsync(scope, cancellationToken);
            defaultSort ??= session.DefaultSort;
            if (session.DefaultSort != defaultSort)
            {
                throw ProviderError(
                    FileBrowserErrorCode.CorruptProviderResponse,
                    "Process-run file roots disagree on their default ordering contract.");
            }

            string[] sourceIds = session.Providers
                .Select(provider => provider.Descriptor.Id.Value)
                .OrderBy(sourceId => sourceId, StringComparer.Ordinal)
                .ToArray();
            revisionParts.Add(string.Join(':', scope.Id.Value, session.Revision.Value, string.Join(',', sourceIds)));
            foreach (IFileBrowserProvider provider in session.Providers)
            {
                if (providers.Count >= ProcessRunFileScopeSet.MaximumScopes)
                {
                    throw ProviderError(
                        FileBrowserErrorCode.InvalidOperation,
                        $"Process-run browsing supports at most {ProcessRunFileScopeSet.MaximumScopes} sources.");
                }

                if (!sourceScopes.TryAdd(provider.Descriptor.Id, scope))
                {
                    throw ProviderError(
                        FileBrowserErrorCode.CorruptProviderResponse,
                        "Process-run browsing produced a duplicate source identifier.");
                }

                sourceActions.Add(
                    provider.Descriptor.Id,
                    FileToolsHostActionCapabilityCapture.Resolve(provider));

                providers.Add(provider);
            }
        }

        if (providers.Count == 0)
        {
            throw ProviderError(FileBrowserErrorCode.NotFound, "The process run has no managed file roots.");
        }

        string revision = BuildRevision(scopeSet.Fingerprint, revisionParts);
        var sourceSet = new FileBrowserSourceSet(revision, providers);
        var options = new FileBrowserSessionOptions(
            pageSize: 50,
            defaultSort: defaultSort ?? DefaultSort,
            retentionMode: FileBrowserStateRetentionMode.Disabled,
            searchBudget: SearchBudget);
        var browser = new FileBrowserSession(sourceSet, options: options);
        logger.LogInformation(
            "Process-run files opened. RunId={RunId} RootCount={RootCount} SourceCount={SourceCount} Revision={Revision} HostCacheMode={HostCacheMode} SessionRetention={SessionRetention}.",
            runId,
            scopeSet.Scopes.Count,
            sourceScopes.Count,
            revision[..12],
            FileToolsHostBrowseCacheMode.Disabled,
            FileBrowserStateRetentionMode.Disabled);
        return new ProcessRunFileWorkspace(runId, browser, sourceScopes, sourceActions, revision);
    }

    public async ValueTask<ProcessRunFileInteraction> ActivateAsync(
        ProcessRunFileWorkspace workspace,
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
                "The selected file source is no longer part of the process run.");
        }

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
            return new ProcessRunFileInteraction(request, session, knownFileSessionReleaser);
        }
        catch
        {
            await knownFileSessionReleaser.ReleaseAsync(activation.Request.File, CancellationToken.None);
            throw;
        }
    }

    private static string BuildRevision(string fingerprint, IReadOnlyList<string> revisionParts)
    {
        string canonical = string.Join('\n',
            [
                "process-run-file-sources-v1",
                fingerprint,
                .. revisionParts
            ]);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static FileBrowserProviderException ProviderError(FileBrowserErrorCode code, string message)
        => new(new FileBrowserError(code, message));
}
