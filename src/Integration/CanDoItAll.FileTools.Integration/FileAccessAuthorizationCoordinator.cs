using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.FileTools.Integration;

public interface IFileAccessPolicy
{
    ValueTask AuthorizeAsync(
        FileAccessGrantRequest request,
        CancellationToken cancellationToken = default);
}

public interface IStorageFileAccessAuthorizationCoordinator
{
    ValueTask<FileReference> GrantAsync(
        FileAccessGrantRequest request,
        StorageObjectReference reference,
        CancellationToken cancellationToken = default);

    ValueTask<AuthorizedStorageFile> ResolveAsync(
        FileReference file,
        FileAccessContext context,
        FileAccessOperation operation,
        CancellationToken cancellationToken = default);

    ValueTask RevokeAsync(FileReference file, CancellationToken cancellationToken = default);

    ValueTask RevokeAllAsync(CancellationToken cancellationToken = default);
}

public sealed record AuthorizedStorageFile(
    StorageCatalogRecord Storage,
    StorageObjectReference Reference,
    FileToolsSemanticScope Scope,
    FileAccessOperation Operations,
    string? ExpectedRevision);

public sealed class LocalWorkspaceFileAccessPolicy(ICanonicalRuntimeDatabase runtimeDatabase) : IFileAccessPolicy
{
    public const string ActorId = "local-workspace";

    public ValueTask AuthorizeAsync(
        FileAccessGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(request.Context.ActorId.Value, ActorId, StringComparison.Ordinal) ||
            request.Context.RuntimeProfileId != runtimeDatabase.Profile.Profile.Id ||
            request.Context.RuntimeGeneration != runtimeDatabase.Generation)
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.Forbidden,
                "The current file access context is not authorized for this runtime.");
        }

        return ValueTask.CompletedTask;
    }
}

public sealed class LocalWorkspaceFileAccessContextProvider(ICanonicalRuntimeDatabase runtimeDatabase)
    : IFileAccessContextProvider
{
    private static readonly FileAccessSessionId RuntimeSessionId = new($"runtime-{Environment.ProcessId}");

    public ValueTask<FileAccessContext> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new FileAccessContext(
            new FileAccessActorId(LocalWorkspaceFileAccessPolicy.ActorId),
            RuntimeSessionId,
            runtimeDatabase.Profile.Profile.Id,
            runtimeDatabase.Generation,
            authorizationRevision: 0,
            new FileAccessCorrelationId(Guid.NewGuid().ToString("N"))));
    }
}

internal sealed class StorageFileAccessAuthorizationCoordinator(
    IFileAccessHandleRegistry registry,
    IFileAccessPolicy policy,
    IStorageCatalogService storageCatalog,
    IStorageDriverRegistry drivers,
    IFileToolsStorageBindingProvider bindingProvider) : IStorageFileAccessAuthorizationCoordinator
{
    public async ValueTask<FileReference> GrantAsync(
        FileAccessGrantRequest request,
        StorageObjectReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(reference);
        StorageCatalogRecord storage = await ResolveStorageAsync(request.StorageId, cancellationToken);
        if (reference.StorageId != storage.Id || reference.ProviderKind != storage.ProviderKind)
        {
            throw Denied(FileAccessFailureCode.SourceUnavailable);
        }

        await EnsureCurrentBindingAsync(
            request.Scope,
            storage,
            request.OccurrenceId,
            cancellationToken);
        await policy.AuthorizeAsync(request, cancellationToken);
        FileAccessGrantRequest effectiveRequest = request;
        if (!drivers.TryResolve(storage.ProviderKind, out IStorageDriver driver))
        {
            throw Denied(FileAccessFailureCode.SourceUnavailable);
        }
        if (request.ExpectedRevision is null && driver is IStorageRevisionedContentDriver revisioned)
        {
            StorageContentRevision? revision = await revisioned.GetRevisionAsync(storage, reference, cancellationToken);
            if (revision is { } value)
            {
                effectiveRequest = new FileAccessGrantRequest(
                    request.Context,
                    request.Scope,
                    request.StorageId,
                    request.OccurrenceId,
                    request.Operations,
                    value.Value);
            }
        }

        FileAccessHandleGrant grant = registry.Issue(effectiveRequest, reference);
        return AuthorizedFileReference.Create(grant.Id, effectiveRequest.ExpectedRevision);
    }

    public async ValueTask<AuthorizedStorageFile> ResolveAsync(
        FileReference file,
        FileAccessContext context,
        FileAccessOperation operation,
        CancellationToken cancellationToken = default)
    {
        FileAccessHandleId id = AuthorizedFileReference.Parse(file);
        FileAccessHandleGrant grant = registry.Resolve(id, context, operation);
        StorageCatalogRecord storage = await ResolveStorageAsync(grant.Request.StorageId, cancellationToken);
        if (grant.Reference.StorageId != storage.Id || grant.Reference.ProviderKind != storage.ProviderKind)
        {
            throw Denied(FileAccessFailureCode.SourceUnavailable);
        }

        await EnsureCurrentBindingAsync(
            grant.Request.Scope,
            storage,
            grant.Request.OccurrenceId,
            cancellationToken);
        var currentRequest = new FileAccessGrantRequest(
            context,
            grant.Request.Scope,
            grant.Request.StorageId,
            grant.Request.OccurrenceId,
            grant.Request.Operations,
            grant.Request.ExpectedRevision);
        await policy.AuthorizeAsync(currentRequest, cancellationToken);
        return new AuthorizedStorageFile(
            storage,
            grant.Reference,
            grant.Request.Scope,
            grant.Request.Operations,
            grant.Request.ExpectedRevision);
    }

    public ValueTask RevokeAsync(FileReference file, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        registry.Revoke(AuthorizedFileReference.Parse(file));
        return ValueTask.CompletedTask;
    }

    public ValueTask RevokeAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        registry.RevokeAll();
        return ValueTask.CompletedTask;
    }

    private async Task<StorageCatalogRecord> ResolveStorageAsync(Guid storageId, CancellationToken cancellationToken)
    {
        StorageCatalogRecord? storage = await storageCatalog.GetAsync(storageId, cancellationToken);
        if (storage is null || !storage.IsEnabled)
        {
            throw Denied(FileAccessFailureCode.SourceUnavailable);
        }

        return storage;
    }

    private async ValueTask EnsureCurrentBindingAsync(
        FileToolsSemanticScope scope,
        StorageCatalogRecord storage,
        string occurrenceId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FileToolsStorageBinding> bindings;
        try
        {
            bindings = await bindingProvider.ResolveAsync(scope, cancellationToken);
        }
        catch (FileBrowserProviderException)
        {
            throw Denied(FileAccessFailureCode.Forbidden);
        }

        FileToolsStorageBinding[] current = bindings
            .Where(binding => binding.StorageId == storage.Id)
            .ToArray();
        if (current.Length != 1 || !IsWithinRoot(current[0].Root, occurrenceId, storage.ProviderKind))
        {
            throw Denied(FileAccessFailureCode.Forbidden);
        }
    }

    private static bool IsWithinRoot(
        FileToolsStorageRoot root,
        string occurrenceId,
        StorageProviderKind providerKind)
    {
        if (root.IsStorageRoot)
        {
            return true;
        }

        string occurrence = occurrenceId.Trim().Replace('\\', '/').Trim('/');
        return string.Equals(occurrence, root.Value, StringComparison.Ordinal) ||
               occurrence.StartsWith(root.Value + "/", StringComparison.Ordinal);
    }

    private static FileAccessDeniedException Denied(FileAccessFailureCode code)
        => new(code, "The authorized file source is unavailable.");
}
