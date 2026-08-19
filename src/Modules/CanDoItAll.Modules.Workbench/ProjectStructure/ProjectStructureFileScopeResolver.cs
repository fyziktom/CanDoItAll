using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureFileScopeResolver(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ProjectStructureAssemblyService assemblyService,
    IStorageCatalogService storageCatalog)
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
        ResolvedKnownFileNode target = await ResolveKnownFileNodeAsync(
            projectId,
            nodeId,
            cancellationToken);
        return await ResolveKnownFileAsync(
            target.Node,
            target.Binding,
            target.ScopeKey,
            cancellationToken);
    }

    public async ValueTask<FileToolsSemanticScope> ResolveNodeCollectionAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        ResolvedCollectionNode target = await ResolveCollectionNodeAsync(
            projectId,
            nodeId,
            cancellationToken);
        NodeCollectionBinding collection = await ResolveNodeCollectionBindingAsync(
            target.Node,
            target.ScopeKey,
            cancellationToken);
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

        if (key.IsProjected)
        {
            return await ResolveProjectedBindingsAsync(key, cancellationToken);
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        ProjectObjectRecord node = await ResolvePersistedScopeNodeAsync(
            dbContext,
            key.ProjectObjectId,
            cancellationToken);
        return key.Mode switch
        {
            ProjectStructureNodeFileScopeMode.KnownFile =>
            [await ResolveKnownFileBindingAsync(
                node,
                await ResolveBindingAsync(dbContext, node.Id, cancellationToken),
                key,
                cancellationToken)],
            ProjectStructureNodeFileScopeMode.Collection =>
            [ToStorageBinding(await ResolveNodeCollectionBindingAsync(node, key, cancellationToken))],
            _ => throw ProviderError(FileBrowserErrorCode.InvalidOperation, "The project-node file scope mode is invalid.")
        };
    }

    private async ValueTask<ResolvedKnownFileNode> ResolveKnownFileNodeAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(nodeId))
        {
            throw ProviderError(FileBrowserErrorCode.InvalidOperation, "The project-node file target is invalid.");
        }

        string normalizedNodeId = nodeId.Trim();
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        ProjectObjectRecord? node = await dbContext.Set<ProjectObjectRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ProjectId == projectId && item.NodeKey == normalizedNodeId,
                cancellationToken);
        if (node is not null)
        {
            return new ResolvedKnownFileNode(
                node,
                await ResolveBindingAsync(dbContext, node.Id, cancellationToken),
                ProjectStructureNodeFileScopeKey.CreatePersisted(
                    ProjectStructureNodeFileScopeMode.KnownFile,
                    node.Id));
        }

        ProjectObjectRecord? projectedNode = await assemblyService.FindNodeAsync(
            dbContext,
            projectId,
            normalizedNodeId,
            cancellationToken);
        if (projectedNode is null || !projectedNode.IsSystemManaged)
        {
            throw ProviderError(
                FileBrowserErrorCode.NotFound,
                "The project-node file target no longer exists.");
        }

        return new ResolvedKnownFileNode(
            projectedNode,
            projectedNode.Binding,
            ProjectStructureNodeFileScopeKey.CreateProjected(
                ProjectStructureNodeFileScopeMode.KnownFile,
                projectId,
                projectedNode.NodeKey));
    }

    private async ValueTask<ResolvedCollectionNode> ResolveCollectionNodeAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(nodeId))
        {
            throw ProviderError(FileBrowserErrorCode.InvalidOperation, "The project-node file target is invalid.");
        }

        string normalizedNodeId = nodeId.Trim();
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        ProjectObjectRecord? node = await dbContext.Set<ProjectObjectRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ProjectId == projectId && item.NodeKey == normalizedNodeId,
                cancellationToken);
        if (node is not null)
        {
            return new ResolvedCollectionNode(
                node,
                ProjectStructureNodeFileScopeKey.CreatePersisted(
                    ProjectStructureNodeFileScopeMode.Collection,
                    node.Id));
        }

        ProjectObjectRecord? projectedNode = await assemblyService.FindNodeAsync(
            dbContext,
            projectId,
            normalizedNodeId,
            cancellationToken);
        if (projectedNode is null || !projectedNode.IsSystemManaged)
        {
            throw ProviderError(
                FileBrowserErrorCode.NotFound,
                "The project-node file target no longer exists.");
        }

        return new ResolvedCollectionNode(
            projectedNode,
            ProjectStructureNodeFileScopeKey.CreateProjected(
                ProjectStructureNodeFileScopeMode.Collection,
                projectId,
                projectedNode.NodeKey));
    }

    private static async ValueTask<ProjectNodeBindingState> ResolveBindingAsync(
        AppDbContext dbContext,
        Guid projectObjectId,
        CancellationToken cancellationToken)
    {
        ProjectNodeBindingRecord? binding = await dbContext.Set<ProjectNodeBindingRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.ProjectObjectId == projectObjectId, cancellationToken);
        if (binding is null)
        {
            throw ProviderError(
                FileBrowserErrorCode.NotFound,
                "The project-node file binding no longer exists.");
        }

        return new ProjectNodeBindingState(
            binding.Route,
            binding.ExternalArtifactKind,
            binding.ExternalArtifactId,
            binding.MediaRelativePath,
            binding.MediaContentType,
            binding.MediaOriginalFileName,
            binding.StorageObjectReferenceJson);
    }

    private async ValueTask<FileToolsStorageBinding> ResolveKnownFileBindingAsync(
        ProjectObjectRecord node,
        ProjectNodeBindingState binding,
        ProjectStructureNodeFileScopeKey scopeKey,
        CancellationToken cancellationToken)
    {
        FileToolsKnownFileScope knownFile = await ResolveKnownFileAsync(
            node,
            binding,
            scopeKey,
            cancellationToken);
        return new FileToolsStorageBinding(
            knownFile.Occurrence.StorageId,
            $"{node.Title} file",
            WorkLimits,
            new FileToolsStorageRoot(ToBindingOccurrenceId(knownFile.Occurrence)),
            FileToolsHostBrowseCacheMode.Disabled);
    }

    private async ValueTask<FileToolsKnownFileScope> ResolveKnownFileAsync(
        ProjectObjectRecord node,
        ProjectNodeBindingState binding,
        ProjectStructureNodeFileScopeKey scopeKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(binding.StorageObjectReferenceJson))
        {
            throw ProviderError(FileBrowserErrorCode.Unsupported, "The node does not identify a supported stored file.");
        }

        if (!StorageJson.TryParseReference(binding.StorageObjectReferenceJson, out StorageObjectReference? reference) ||
            reference is null)
        {
            throw ProviderError(FileBrowserErrorCode.Forbidden, "The stored file locator is invalid.");
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
        Guid storageId = await ResolveStorageIdAsync(
            node,
            binding,
            reference,
            scopeKey,
            occurrenceId,
            cancellationToken);
        string fileName = ResolveFileName(binding, reference, occurrenceId);
        string? mediaType = ResolveMediaType(binding, reference);
        var occurrence = new FileToolsKnownFileOccurrence(
            storageId,
            occurrenceKind,
            occurrenceId,
            fileName,
            mediaType,
            reference.ContentLength);
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.ProjectNode,
            scopeKey.ToScopeId(),
            node.Title);
        return new FileToolsKnownFileScope(scope, occurrence);
    }

    private async ValueTask<IReadOnlyList<FileToolsStorageBinding>> ResolveProjectedBindingsAsync(
        ProjectStructureNodeFileScopeKey key,
        CancellationToken cancellationToken)
    {
        if (key.Mode is not ProjectStructureNodeFileScopeMode.KnownFile and
            not ProjectStructureNodeFileScopeMode.Collection ||
            key.ProjectId == Guid.Empty ||
            string.IsNullOrWhiteSpace(key.NodeKey))
        {
            throw ProviderError(
                FileBrowserErrorCode.InvalidOperation,
                "The projected project-node file scope identifier is invalid.");
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        ProjectObjectRecord? node = await assemblyService.FindNodeAsync(
            dbContext,
            key.ProjectId,
            key.NodeKey,
            cancellationToken);
        if (node is null || !node.IsSystemManaged)
        {
            throw ProviderError(
                FileBrowserErrorCode.Conflict,
                "The projected project-node file scope is no longer current.");
        }

        return key.Mode switch
        {
            ProjectStructureNodeFileScopeMode.KnownFile =>
            [await ResolveKnownFileBindingAsync(
                node,
                node.Binding,
                key,
                cancellationToken)],
            ProjectStructureNodeFileScopeMode.Collection =>
            [ToStorageBinding(await ResolveNodeCollectionBindingAsync(node, key, cancellationToken))],
            _ => throw ProviderError(
                FileBrowserErrorCode.InvalidOperation,
                "The projected project-node file scope identifier is invalid.")
        };
    }

    private static async ValueTask<ProjectObjectRecord> ResolvePersistedScopeNodeAsync(
        AppDbContext dbContext,
        Guid projectObjectId,
        CancellationToken cancellationToken)
    {
        ProjectObjectRecord? node = await dbContext.Set<ProjectObjectRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == projectObjectId, cancellationToken);
        return node ?? throw ProviderError(
            FileBrowserErrorCode.NotFound,
            "The project-node file scope no longer exists.");
    }

    private async ValueTask<Guid> ResolveStorageIdAsync(
        ProjectObjectRecord node,
        ProjectNodeBindingState binding,
        StorageObjectReference reference,
        ProjectStructureNodeFileScopeKey scopeKey,
        string occurrenceId,
        CancellationToken cancellationToken)
    {
        if (reference.StorageId is Guid storageId && storageId != Guid.Empty)
        {
            return storageId;
        }

        if (reference.ProviderKind != StorageProviderKind.FileSystem ||
            reference.LocatorKind != StorageLocatorKind.RelativePath ||
            !IsProjectedProcessScreenshot(
                node,
                binding,
                scopeKey,
                occurrenceId))
        {
            throw ProviderError(
                FileBrowserErrorCode.Unsupported,
                "The node does not identify a supported stored file.");
        }

        return await ResolveWorkspaceStorageIdAsync(cancellationToken);
    }

    private static bool IsProjectedProcessScreenshot(
        ProjectObjectRecord node,
        ProjectNodeBindingState binding,
        ProjectStructureNodeFileScopeKey scopeKey,
        string occurrenceId)
    {
        if (!scopeKey.IsProjected ||
            scopeKey.ProjectId != node.ProjectId ||
            !string.Equals(scopeKey.NodeKey, node.NodeKey, StringComparison.Ordinal) ||
            !node.IsSystemManaged ||
            node.ObjectType != ProjectObjectType.ImageAsset ||
            !string.Equals(
                binding.ExternalArtifactKind,
                ProjectStructureProcessNodeKeys.ProcessRunScreenshotArtifactKind,
                StringComparison.Ordinal) ||
            !ProjectStructureProcessNodeKeys.TryParseProcessRunScreenshotNodeKey(node.NodeKey, out Guid runId) ||
            binding.ExternalArtifactId != runId ||
            !string.Equals(
                node.NodeKey,
                ProjectStructureProcessNodeKeys.BuildProcessRunScreenshotNodeKey(runId, occurrenceId),
                StringComparison.Ordinal) ||
            !string.Equals(
                NormalizeStorageRelativeValue(
                    binding.MediaRelativePath,
                    FileToolsKnownFileOccurrenceKind.RelativePath),
                occurrenceId,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string managedArtifactRoot = ProcessLaunchApplicationService.BuildManagedProcessArtifactRoot(
            new ProcessRunId(runId));
        return occurrenceId.StartsWith(
            $"{managedArtifactRoot}/",
            StringComparison.OrdinalIgnoreCase);
    }

    private async ValueTask<NodeCollectionBinding> ResolveNodeCollectionBindingAsync(
        ProjectObjectRecord node,
        ProjectStructureNodeFileScopeKey scopeKey,
        CancellationToken cancellationToken)
    {
        if (scopeKey.Mode != ProjectStructureNodeFileScopeMode.Collection)
        {
            throw ProviderError(FileBrowserErrorCode.InvalidOperation, "The project-node collection scope is invalid.");
        }

        if (scopeKey.IsProjected)
        {
            return await ResolveProjectedProcessRunOutputBindingAsync(node, scopeKey, cancellationToken);
        }

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
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.ProjectNode,
            scopeKey.ToScopeId(),
            node.Title);
        return new NodeCollectionBinding(scope, storageId, node.Title, prefix);
    }

    private async ValueTask<NodeCollectionBinding> ResolveProjectedProcessRunOutputBindingAsync(
        ProjectObjectRecord node,
        ProjectStructureNodeFileScopeKey scopeKey,
        CancellationToken cancellationToken)
    {
        if (scopeKey.ProjectId != node.ProjectId ||
            !string.Equals(scopeKey.NodeKey, node.NodeKey, StringComparison.Ordinal) ||
            !ProjectStructureProcessRunOutputFolderPolicy.TryResolve(node, out var outputFolder))
        {
            throw ProviderError(
                FileBrowserErrorCode.Forbidden,
                "The projected node is not an authorized process-run folder collection.");
        }

        Guid storageId = await ResolveWorkspaceStorageIdAsync(cancellationToken);
        var scope = new FileToolsSemanticScope(
            FileToolsSemanticScopeKind.ProjectNode,
            scopeKey.ToScopeId(),
            node.Title);
        string scopedDirectoryPath = ProjectStructureProcessRunOutputFolderPolicy.ResolveProjectScopedDirectoryPath(
            scopeKey.ProjectId,
            outputFolder);
        return new NodeCollectionBinding(scope, storageId, node.Title, scopedDirectoryPath);
    }

    private async ValueTask<Guid> ResolveWorkspaceStorageIdAsync(CancellationToken cancellationToken)
    {
        StorageCatalogRecord workspaceStorage = await storageCatalog
            .EnsureBootstrapFileSystemStorageAsync(cancellationToken);
        if (workspaceStorage.Id == Guid.Empty ||
            !workspaceStorage.IsEnabled ||
            workspaceStorage.ProviderKind != StorageProviderKind.FileSystem)
        {
            throw ProviderError(
                FileBrowserErrorCode.CorruptProviderResponse,
                "The workspace storage catalog is not a usable filesystem source.");
        }

        return workspaceStorage.Id;
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

        string normalized;
        if (isPath)
        {
            try
            {
                normalized = LogicalPath.ParseLegacyWindowsLogicalPath(candidate).Value;
            }
            catch (ArgumentException)
            {
                throw ProviderError(FileBrowserErrorCode.Forbidden, "The node file metadata escapes its storage scope.");
            }
        }
        else
        {
            normalized = candidate;
        }

        if (normalized.Length > FileToolsKnownFileOccurrence.MaximumOccurrenceIdLength)
        {
            throw ProviderError(FileBrowserErrorCode.Forbidden, "The node file metadata escapes its storage scope.");
        }

        return normalized;
    }

    private static string ResolveFileName(
        ProjectNodeBindingState binding,
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
        ProjectNodeBindingState binding,
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

    private sealed record ResolvedKnownFileNode(
        ProjectObjectRecord Node,
        ProjectNodeBindingState Binding,
        ProjectStructureNodeFileScopeKey ScopeKey);

    private sealed record ResolvedCollectionNode(
        ProjectObjectRecord Node,
        ProjectStructureNodeFileScopeKey ScopeKey);
}
