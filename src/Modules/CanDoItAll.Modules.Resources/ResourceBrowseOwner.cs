using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Resources;

internal sealed class ResourceBrowseOwner(IResourceFileSourceCatalog sources, ResourceFileBrowseCoordinator browse,
    ResourceStorageObjectPromotionService promotion, ResourceStorageObjectInteractionService interaction,
    IFileToolsBrowseSessionFactory sessions, IFileToolsBrowseItemActionService actions, ICanonicalRuntimeDatabase canonical) : IResourceBrowseOwner {
    private readonly Guid profile = canonical.Profile.Profile.Id;
    private readonly long generation = canonical.Generation;
    public bool IsCurrent => canonical.Profile.Profile.Id == profile && canonical.Generation == generation;
    public bool IsLocalLaunchAvailable => actions.IsLocalLaunchAvailable;
    public async Task<ResourceBrowseCatalog> LoadAsync(CancellationToken cancellationToken = default) {
        RequireCurrent();
        var catalog = await sources.LoadAsync(cancellationToken);
        return new(catalog.Sources.Select(Project).ToArray(), catalog.Projects.Select(p => new ResourceProjectOption(p.Id, p.Name, p.Admission)).ToArray(), catalog.Fingerprint);
    }
    public async ValueTask<ResourceBrowseLease> OpenAsync(ResourceFileSourceKey key, CancellationToken cancellationToken = default) {
        RequireCurrent();
        var opened = await browse.OpenAsync(key, cancellationToken);
        return new(Project(opened.Source), opened.Browser, opened.ActionAvailability, opened.Revision, opened.DisposeAsync);
    }
    public async ValueTask<ResourcePreviewLease> OpenResourceAsync(Guid resourceId, CancellationToken cancellationToken = default) {
        RequireCurrent();
        var opened = await interaction.OpenAsync(resourceId, cancellationToken);
        return new(opened.ResourceId, opened.Request, opened.Session.ContentSource, opened.DisposeAsync);
    }
    public async ValueTask<ResourcePromotionObservation> PromoteAsync(ResourcePromotionRequest command) {
        await RequireSelectionAsync(command.Selection, CancellationToken.None);
        try {
            var result = await promotion.PromoteAsync(new(command.Selection.Source.Key, command.Selection.Item.Key,
                command.Project.ProjectId, command.Name, command.Project, command.Sensitivity));
            return new(result.ResourceId, result.Created, result.Revision.Scope);
        } catch (ResourcePromotionCommittedException exception) {
            return new(exception.ResourceId, exception.Created, exception.Revision?.Scope, exception.Message);
        } catch (ResourcePromotionException exception) when (exception.Code != ResourcePromotionFailureCode.PersistenceFailed) {
            throw new ResourceActionRefusedException(exception.Message);
        }
    }
    public async ValueTask<FileToolsBrowseItemActionResult> LaunchAsync(ResourceFileSelection selection, FileToolsLocalFileAction action, CancellationToken cancellationToken = default) {
        await RequireSelectionAsync(selection, cancellationToken);
        return await actions.LaunchAsync(selection.Source.Scope, selection.Item.Key, action, cancellationToken);
    }
    public async ValueTask<IFileToolsDownloadLease> AuthorizeDownloadAsync(ResourceFileSelection selection, CancellationToken cancellationToken = default) {
        await RequireSelectionAsync(selection, cancellationToken);
        return await actions.AuthorizeDownloadAsync(selection.Source.Scope, selection.Item.Key, cancellationToken);
    }
    private async Task RequireSelectionAsync(ResourceFileSelection selection, CancellationToken cancellationToken) {
        RequireCurrent();
        var current = await sources.ResolveAsync(selection.Source.Key, cancellationToken);
        if (current.Scope != selection.Source.Scope) {
            throw new ResourceActionRefusedException("The selected source configuration changed. Refresh and select the file again.");
        }
        var session = await sessions.CreateAsync(current.Scope, cancellationToken);
        if (session.Revision.Value != selection.Revision || !session.Providers.Any(p => p.Descriptor.Id == selection.Item.Key.SourceId)) {
            throw new ResourceActionRefusedException("The selected file source revision changed. Refresh and select the file again.");
        }
        RequireCurrent();
    }
    private void RequireCurrent() {
        if (!IsCurrent) {
            throw new ResourceActionRefusedException("The database profile changed. Open a new Resources workspace.");
        }
    }
    private static ResourceBrowseSource Project(ResourceFileSourceDescriptor source) => new(source.Key, source.SourceClass,
        source.DisplayName, source.Detail, source.Scope, source.StorageId, source.IsReadOnly, source.HealthStatus switch {
            null => null,
            StorageHealthStatus.Unknown => ResourceSourceHealth.Unknown,
            StorageHealthStatus.Healthy => ResourceSourceHealth.Healthy,
            StorageHealthStatus.Degraded => ResourceSourceHealth.Degraded,
            StorageHealthStatus.Unavailable => ResourceSourceHealth.Unavailable,
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        });
}
