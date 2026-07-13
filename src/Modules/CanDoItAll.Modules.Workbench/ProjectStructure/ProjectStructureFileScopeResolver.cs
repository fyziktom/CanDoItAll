using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureFileScopeResolver(
    IDbContextFactory<AppDbContext> dbContextFactory)
    : IFileToolsStorageBindingSource, IProjectStructureNodeFileScopeProvider
{
    private static readonly FileToolsBrowseWorkLimits WorkLimits = new(
        maximumReturnedItems: 50,
        maximumInspectedItems: 2_000,
        maximumMetadataProbes: 50,
        maximumConcurrentMetadataProbes: 1,
        maximumDuration: TimeSpan.FromSeconds(5));

    public FileToolsSemanticScopeKind ScopeKind => FileToolsSemanticScopeKind.ProjectNode;

    public async ValueTask<FileToolsKnownFileScope> ResolveKnownFileAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        ProjectObjectRecord node = await ResolveNodeAsync(projectId, nodeId, cancellationToken);
        ProjectNodeBindingRecord binding = await ResolveBindingAsync(node.Id, cancellationToken);
        return ResolveKnownFile(node, binding);
    }

    public async ValueTask<FileToolsSemanticScope> ResolveNodeCollectionAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        ProjectObjectRecord node = await ResolveNodeAsync(projectId, nodeId, cancellationToken);
        NodeCollectionBinding collection = await ResolveNodeCollectionBindingAsync(node, cancellationToken);
        return collection.Scope;
    }

    public async ValueTask<IReadOnlyList<FileToolsStorageBinding>> ResolveAsync(
        FileToolsSemanticScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Kind != ScopeKind ||
            !ProjectStructureNodeFileScopeKey.TryParse(scope.Id, out ProjectStructureNodeFileScopeKey key))
        {
            throw ProviderError(FileBrowserErrorCode.InvalidOperation, "The project-node file scope identifier is invalid.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        ProjectObjectRecord? node = await dbContext.Set<ProjectObjectRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == key.ProjectObjectId, cancellationToken);
        if (node is null)
        {
            throw ProviderError(FileBrowserErrorCode.NotFound, "The project-node file scope no longer exists.");
        }

        return key.Mode switch
        {
            ProjectStructureNodeFileScopeMode.KnownFile =>
            [await ResolveKnownFileBindingAsync(node, cancellationToken)],
            ProjectStructureNodeFileScopeMode.Collection =>
            [ToStorageBinding(await ResolveNodeCollectionBindingAsync(node, cancellationToken))],
            _ => throw ProviderError(FileBrowserErrorCode.InvalidOperation, "The project-node file scope mode is invalid.")
        };
    }

    private async ValueTask<ProjectObjectRecord> ResolveNodeAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(nodeId))
        {
            throw ProviderError(FileBrowserErrorCode.InvalidOperation, "The project-node file target is invalid.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        ProjectObjectRecord? node = await dbContext.Set<ProjectObjectRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ProjectId == projectId && item.NodeKey == nodeId,
                cancellationToken);
        return node ?? throw ProviderError(
            FileBrowserErrorCode.NotFound,
            "The project-node file target no longer exists.");
    }

    private async ValueTask<ProjectNodeBindingRecord> ResolveBindingAsync(
        Guid projectObjectId,
        CancellationToken cancellationToken)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        ProjectNodeBindingRecord? binding = await dbContext.Set<ProjectNodeBindingRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.ProjectObjectId == projectObjectId, cancellationToken);
        return binding ?? throw ProviderError(
            FileBrowserErrorCode.NotFound,
            "The project-node file binding no longer exists.");
    }

    private async ValueTask<FileToolsStorageBinding> ResolveKnownFileBindingAsync(
        ProjectObjectRecord node,
        CancellationToken cancellationToken)
    {
        ProjectNodeBindingRecord binding = await ResolveBindingAsync(node.Id, cancellationToken);
        FileToolsKnownFileScope knownFile = ResolveKnownFile(node, binding);
        return new FileToolsStorageBinding(
            knownFile.Occurrence.StorageId,
            $"{node.Title} file",
            WorkLimits,
            new FileToolsStorageRoot(ToBindingOccurrenceId(knownFile.Occurrence)),
            FileToolsHostBrowseCacheMode.Disabled);
    }

    private static FileToolsKnownFileScope ResolveKnownFile(
        ProjectObjectRecord node,
        ProjectNodeBindingRecord binding)
    {
        if (!StorageJson.TryParseReference(binding.StorageObjectReferenceJson, out StorageObjectReference? reference) ||
            reference?.StorageId is not Guid storageId ||
            storageId == Guid.Empty)
        {
            throw ProviderError(FileBrowserErrorCode.Unsupported, "The node does not identify a supported stored file.");
        }

        FileToolsKnownFileOccurrenceKind occurrenceKind = (reference.ProviderKind, reference.LocatorKind) switch
        {
            (StorageProviderKind.FileSystem, StorageLocatorKind.RelativePath) => FileToolsKnownFileOccurrenceKind.RelativePath,
            (StorageProviderKind.Ipfs, StorageLocatorKind.ContentAddress) => FileToolsKnownFileOccurrenceKind.ContentAddress,
            (StorageProviderKind.Ipfs, StorageLocatorKind.RemotePath) => FileToolsKnownFileOccurrenceKind.RemotePath,
            (StorageProviderKind.Ftp, StorageLocatorKind.RemotePath) => FileToolsKnownFileOccurrenceKind.RemotePath,
            _ => throw ProviderError(FileBrowserErrorCode.Unsupported, "The node storage locator is not supported for file interaction.")
        };
        string occurrenceId = NormalizeStorageRelativeValue(reference.Locator, occurrenceKind);
        string fileName = ResolveFileName(binding, reference, occurrenceId);
        string? mediaType = ResolveMediaType(binding, reference);
        var occurrence = new FileToolsKnownFileOccurrence(
            storageId,
            occurrenceKind,
            occurrenceId,
            fileName,
            mediaType,
            reference.ContentLength);
        var key = new ProjectStructureNodeFileScopeKey(
            ProjectStructureNodeFileScopeMode.KnownFile,
            node.Id);
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.ProjectNode,
            key.ToScopeId(),
            node.Title);
        return new FileToolsKnownFileScope(scope, occurrence);
    }

    private async ValueTask<NodeCollectionBinding> ResolveNodeCollectionBindingAsync(
        ProjectObjectRecord node,
        CancellationToken cancellationToken)
    {
        if (node.ObjectType != ProjectObjectType.Infrastructure)
        {
            throw ProviderError(FileBrowserErrorCode.Unsupported, "Only storage-backed infrastructure nodes expose file collections.");
        }

        ProjectObjectMetadataEnvelope metadata;
        try
        {
            metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        }
        catch (InvalidOperationException)
        {
            throw ProviderError(FileBrowserErrorCode.InvalidOperation, "The infrastructure node metadata is invalid.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        string[] storageReferenceIds = await dbContext.Set<ProjectNodeReferenceRecord>()
            .AsNoTracking()
            .Where(item =>
                item.ProjectObjectId == node.Id &&
                item.ReferenceKind == ProjectNodeReferenceKinds.InfrastructureStorageCatalog)
            .OrderBy(item => item.OrderIndex)
            .Select(item => item.ReferenceId)
            .ToArrayAsync(cancellationToken);
        if (storageReferenceIds.Length != 1 ||
            !Guid.TryParse(storageReferenceIds[0], out Guid storageId) ||
            storageId == Guid.Empty)
        {
            throw ProviderError(FileBrowserErrorCode.Unsupported, "The infrastructure node does not identify one storage catalog.");
        }

        string prefix = NormalizeCollectionPrefix(metadata.Infrastructure?.StoragePathPrefix);
        var key = new ProjectStructureNodeFileScopeKey(
            ProjectStructureNodeFileScopeMode.Collection,
            node.Id);
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.ProjectNode,
            key.ToScopeId(),
            node.Title);
        return new NodeCollectionBinding(scope, storageId, node.Title, prefix);
    }

    private static FileToolsStorageBinding ToStorageBinding(NodeCollectionBinding collection)
        => new(
            collection.StorageId,
            $"{collection.DisplayName} files",
            WorkLimits,
            string.IsNullOrEmpty(collection.Prefix)
                ? FileToolsStorageRoot.StorageRoot
                : new FileToolsStorageRoot(collection.Prefix),
            FileToolsHostBrowseCacheMode.Disabled);

    private static string NormalizeCollectionPrefix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return NormalizeStorageRelativeValue(value, FileToolsKnownFileOccurrenceKind.RelativePath);
    }

    private static string NormalizeStorageRelativeValue(
        string value,
        FileToolsKnownFileOccurrenceKind occurrenceKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string candidate = value.Trim();
        bool isPath = occurrenceKind != FileToolsKnownFileOccurrenceKind.ContentAddress;
        if (candidate.StartsWith('/') ||
            candidate.StartsWith('\\') ||
            candidate.StartsWith("//", StringComparison.Ordinal) ||
            candidate.StartsWith("\\\\", StringComparison.Ordinal) ||
            (isPath && candidate.Length >= 2 && char.IsLetter(candidate[0]) && candidate[1] == ':') ||
            (isPath && Uri.TryCreate(candidate, UriKind.Absolute, out _)))
        {
            throw ProviderError(FileBrowserErrorCode.Forbidden, "The node file metadata must be storage-relative.");
        }

        string normalized = candidate.Replace('\\', '/').Trim('/');
        if (normalized.Length == 0 ||
            normalized.Length > FileToolsKnownFileOccurrence.MaximumOccurrenceIdLength ||
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment is "." or ".."))
        {
            throw ProviderError(FileBrowserErrorCode.Forbidden, "The node file metadata escapes its storage scope.");
        }

        return normalized;
    }

    private static string ResolveFileName(
        ProjectNodeBindingRecord binding,
        StorageObjectReference reference,
        string occurrenceId)
    {
        string fileName = !string.IsNullOrWhiteSpace(binding.MediaOriginalFileName)
            ? binding.MediaOriginalFileName
            : !string.IsNullOrWhiteSpace(reference.DisplayName)
                ? reference.DisplayName
                : Path.GetFileName(occurrenceId);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw ProviderError(FileBrowserErrorCode.InvalidOperation, "The node stored file name is invalid.");
        }

        return fileName;
    }

    private static string? ResolveMediaType(
        ProjectNodeBindingRecord binding,
        StorageObjectReference reference)
        => !string.IsNullOrWhiteSpace(binding.MediaContentType)
            ? binding.MediaContentType
            : !string.IsNullOrWhiteSpace(reference.ContentType)
                ? reference.ContentType
                : null;

    private static string ToBindingOccurrenceId(FileToolsKnownFileOccurrence occurrence)
        => occurrence.Kind == FileToolsKnownFileOccurrenceKind.ContentAddress
            ? $"cid:{occurrence.OccurrenceId}"
            : occurrence.OccurrenceId;

    private static FileBrowserProviderException ProviderError(FileBrowserErrorCode code, string message)
        => new(new FileBrowserError(code, message));

    private sealed record NodeCollectionBinding(
        FileToolsSemanticScope Scope,
        Guid StorageId,
        string DisplayName,
        string Prefix);
}
