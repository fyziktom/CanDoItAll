using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public sealed class WorkbenchProjectStructureRuntimeGateway(
    ProjectsService projectsService,
    ProjectWorkbenchService projectWorkbenchService,
    ProjectStructureLeaseService leaseService,
    IProjectStructureRuntimeLauncher runtimeLauncher,
    IProjectStructureLocalFileOpener localFileOpener,
    ProjectStructureSourceWorkspacePathResolver sourceWorkspacePathResolver) : IProjectStructureRuntimeGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> IdempotentMutationLocks = new(StringComparer.Ordinal);
    private static readonly ProjectStructureRuntimeReadRequest FullNodeReadRequest = new(
        IncludeLayout: true,
        IncludeMetadata: true,
        IncludeNotes: true,
        IncludeAssets: true);

    public async Task<IReadOnlyList<ProjectStructureRuntimeProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default)
    {
        var projects = await projectsService.ListAsync(cancellationToken);
        return projects.Select(MapProject).ToList();
    }

    public async Task<ProjectStructureRuntimeReadResponse> ReadStructureAsync(
        Guid projectId,
        ProjectStructureRuntimeReadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var effectivePriorities = ProjectStructureChecklistRules.BuildEffectivePriorityMap(surface.Nodes);
        var includedNodeIds = ResolveIncludedNodeIds(surface.Nodes, request);
        var selectedNodes = surface.Nodes
            .Where(node => includedNodeIds is null || includedNodeIds.Contains(node.Id))
            .Where(node => request.ObjectTypes is null || request.ObjectTypes.Count == 0 || request.ObjectTypes.Contains(node.ObjectType))
            .Where(node => request.ProjectRoles is null || request.ProjectRoles.Count == 0 || request.ProjectRoles.Contains(MapProjectRole(node.ProjectRole)))
            .Where(node => request.Statuses is null || request.Statuses.Count == 0 || request.Statuses.Contains(node.Status, StringComparer.OrdinalIgnoreCase))
            .Where(node => !request.OnlyUnfinished || !ProjectStructureChecklistRules.IsFinished(node))
            .Where(node => !request.MaxPriority.HasValue || effectivePriorities.GetValueOrDefault(node.Id) > 0 && effectivePriorities[node.Id] <= request.MaxPriority.Value)
            .OrderBy(node => node.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var warnings = new List<string>();

        if (request.Take.HasValue && selectedNodes.Count > request.Take.Value)
        {
            selectedNodes = selectedNodes.Take(Math.Max(1, request.Take.Value)).ToList();
            warnings.Add($"Structure result truncated to {request.Take.Value} nodes.");
        }

        var selectedIds = selectedNodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var links = request.IncludeLinks
            ? surface.Links
                .Where(link => selectedIds.Contains(link.SourceId) && selectedIds.Contains(link.TargetId))
                .Select(MapLink)
                .ToList()
            : [];

        return new ProjectStructureRuntimeReadResponse(
            surface.ProjectId,
            surface.ProjectName,
            selectedNodes
                .Select(node => MapNode(node, effectivePriorities.GetValueOrDefault(node.Id), request))
                .ToList(),
            links,
            warnings);
    }

    public async Task<ProjectStructureRuntimeNodeSummary> CreateNodeAsync(
        Guid projectId,
        ProjectStructureRuntimeNodeCreateRequest request,
        ProjectStructureRuntimeAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(agent);

        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        return await RunWithIdempotencyLockAsync(
            projectId,
            idempotencyKey,
            async cancellationToken => await leaseService.RunWithProjectMutationLeaseAsync(
                projectId,
                request.LeaseToken,
                MapAgent(agent),
                "create-runtime-structure-node",
                async cancellationToken =>
                {
                    if (idempotencyKey is not null &&
                        await TryFindExistingIdempotentNodeAsync(projectId, idempotencyKey, cancellationToken) is { } existingNode)
                    {
                        return existingNode;
                    }

                    var objectSubtype = ProjectStructureRequestedNodeKindParser.NormalizeSubtypeForType(request.ObjectType, request.ObjectSubtype);
                    var metadataJson = ProjectStructureDotNetRuntimeMetadataHydrator.NormalizeMetadataJson(
                        request.ObjectType,
                        objectSubtype,
                        request.Notes,
                        request.MetadataJson);
                    var createdNode = await projectWorkbenchService.CreateObjectAsync(
                        projectId,
                        new ProjectObjectCreateRequest(
                            request.ObjectType,
                            request.Title,
                            request.Subtitle,
                            request.Notes,
                            request.ParentNodeKey,
                            request.X,
                            request.Y,
                            request.StartUtc,
                            request.EndUtc,
                            objectSubtype,
                            MapMedia(request.Media),
                            BuildIdempotentMetadataJson(metadataJson, idempotencyKey, request.IdempotencyBatchKey),
                            request.DurationSeconds),
                        cancellationToken);
                    return MapNode(createdNode, createdNode.Priority, FullNodeReadRequest);
                },
                cancellationToken),
            cancellationToken);
    }

    public async Task<ProjectStructureRuntimeNodeSummary> CreateAssetAsync(
        Guid projectId,
        ProjectStructureRuntimeAssetCreateRequest request,
        ProjectStructureRuntimeAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(agent);

        if (request.ObjectType is not (ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset))
        {
            throw new ProjectStructureAgentException(400, "AssetTypeRequired", "Asset nodes must be File, ImageAsset, or VideoAsset.");
        }

        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        var media = await ResolveAssetCreateMediaAsync(request, cancellationToken);
        return await RunWithIdempotencyLockAsync(
            projectId,
            idempotencyKey,
            async cancellationToken => await leaseService.RunWithProjectMutationLeaseAsync(
                projectId,
                request.LeaseToken,
                MapAgent(agent),
                "create-runtime-structure-asset",
                async cancellationToken =>
                {
                    if (idempotencyKey is not null &&
                        await TryFindExistingIdempotentNodeAsync(projectId, idempotencyKey, cancellationToken) is { } existingNode)
                    {
                        return existingNode;
                    }

                    var createdNode = await projectWorkbenchService.CreateObjectAsync(
                        projectId,
                        new ProjectObjectCreateRequest(
                            request.ObjectType,
                            request.Title,
                            request.Subtitle,
                            request.Notes,
                            request.ParentNodeKey,
                            ObjectSubtype: ProjectStructureRequestedNodeKindParser.NormalizeSubtypeForType(request.ObjectType, request.ObjectSubtype),
                            Media: media,
                            MetadataJson: BuildIdempotentMetadataJson(request.MetadataJson, idempotencyKey, request.IdempotencyBatchKey)),
                        cancellationToken);
                    return MapNode(createdNode, createdNode.Priority, FullNodeReadRequest);
                },
                cancellationToken),
            cancellationToken);
    }

    private async Task<ProjectStructureRuntimeNodeSummary?> TryFindExistingIdempotentNodeAsync(
        Guid projectId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        var existingNode = surface.Nodes.FirstOrDefault(node => HasIdempotencyKey(node.MetadataJson, idempotencyKey));
        return existingNode is null
            ? null
            : MapNode(existingNode, existingNode.Priority, FullNodeReadRequest);
    }

    private static async Task<T> RunWithIdempotencyLockAsync<T>(
        Guid projectId,
        string? idempotencyKey,
        Func<CancellationToken, Task<T>> callback,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey is null)
        {
            return await callback(cancellationToken);
        }

        var lockKey = $"{projectId:D}:{idempotencyKey}";
        var semaphore = IdempotentMutationLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await callback(cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static string? NormalizeIdempotencyKey(string? idempotencyKey)
        => string.IsNullOrWhiteSpace(idempotencyKey)
            ? null
            : idempotencyKey.Trim();

    private static string? NormalizeIdempotencyBatchKey(string? idempotencyBatchKey)
        => string.IsNullOrWhiteSpace(idempotencyBatchKey)
            ? null
            : idempotencyBatchKey.Trim();

    private static string? BuildIdempotentMetadataJson(
        string? metadataJson,
        string? idempotencyKey,
        string? idempotencyBatchKey)
    {
        if (idempotencyKey is null && string.IsNullOrWhiteSpace(idempotencyBatchKey))
        {
            return metadataJson;
        }

        var root = ParseMetadataObject(metadataJson);
        var metadata = root[ProjectStructureRuntimeIdempotencyMetadata.MetadataPropertyName] as JsonObject ?? new JsonObject();
        if (idempotencyKey is not null)
        {
            metadata[ProjectStructureRuntimeIdempotencyMetadata.IdempotencyKeyPropertyName] = idempotencyKey;
        }

        if (NormalizeIdempotencyBatchKey(idempotencyBatchKey) is { } batchKey)
        {
            metadata[ProjectStructureRuntimeIdempotencyMetadata.BatchIdempotencyKeyPropertyName] = batchKey;
        }

        root[ProjectStructureRuntimeIdempotencyMetadata.MetadataPropertyName] = metadata;
        return root.ToJsonString(JsonOptions);
    }

    private static JsonObject ParseMetadataObject(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(metadataJson) as JsonObject
                   ?? throw new InvalidOperationException("Project-structure idempotent write metadata must be a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Project-structure idempotent write metadata must be valid JSON.", exception);
        }
    }

    private static bool HasIdempotencyKey(string? metadataJson, string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty(ProjectStructureRuntimeIdempotencyMetadata.MetadataPropertyName, out var metadata) &&
                   metadata.ValueKind == JsonValueKind.Object &&
                   metadata.TryGetProperty(ProjectStructureRuntimeIdempotencyMetadata.IdempotencyKeyPropertyName, out var key) &&
                   key.ValueKind == JsonValueKind.String &&
                   string.Equals(key.GetString(), idempotencyKey, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ProjectStructureRuntimeProjectSummary MapProject(ProjectSummary project)
        => new(
            project.Id,
            project.Name,
            MapProjectStatus(project.Status),
            project.CurrentPhase,
            project.PhaseCount,
            project.ParentCount,
            project.ChildCount,
            project.UpdatedAtUtc,
            project.PrimaryCustomerName,
            project.PrimaryDeliveryUnitName,
            project.PrimaryOwnerName,
            project.RelatedPartySearchText);

    private ProjectStructureRuntimeNodeSummary MapNode(
        ProjectStructureNode node,
        int effectivePriority,
        ProjectStructureRuntimeReadRequest options)
        => new(
            node.Id,
            node.ParentId,
            node.ObjectType,
            node.ObjectSubtype,
            node.Title,
            node.Subtitle,
            node.Status,
            options.IncludeNotes ? node.Notes : null,
            node.Route,
            node.ArtifactKind,
            node.ArtifactId,
            options.IncludeAssets ? node.MediaRelativePath : null,
            options.IncludeAssets ? node.MediaContentType : null,
            options.IncludeAssets ? node.MediaOriginalFileName : null,
            node.Badges,
            node.ProgressMode,
            node.ProgressPercent,
            node.MarkerIcon,
            node.MarkerTone,
            node.MarkerLabel,
            node.Priority,
            effectivePriority,
            node.StartUtc,
            node.EndUtc,
            options.IncludeMetadata ? node.MetadataJson : null,
            MapProjectRole(node.ProjectRole),
            node.RelatedProjectId,
            node.ParentProjectCount,
            options.IncludeLayout ? node.X : null,
            options.IncludeLayout ? node.Y : null,
            node.DurationSeconds,
            MapActionCapabilities(ProjectStructureNodeActionCapabilityResolver.Resolve(node, runtimeLauncher, localFileOpener)));

    private static ProjectStructureRuntimeLinkSummary MapLink(ProjectStructureLink link)
        => new(link.SourceId, link.TargetId, link.Kind, link.IsUserAuthored);

    private static ProjectStructureRuntimeNodeActionCapabilities? MapActionCapabilities(ProjectStructureNodeActionCapabilities? capabilities)
    {
        if (capabilities is null)
        {
            return null;
        }

        return new ProjectStructureRuntimeNodeActionCapabilities(
            capabilities.CanRunNormally,
            capabilities.CanRunAsAdministrator,
            capabilities.CanOpenInFileExplorer,
            capabilities.CanOpenInNewTab,
            capabilities.RuntimeDisplayName,
            capabilities.RuntimeDisplayCommand,
            capabilities.RuntimeWorkingDirectory,
            capabilities.OpenInNewTabRoute,
            capabilities.StorageProvider,
            capabilities.StorageLocatorKind,
            capabilities.StorageLocator,
            capabilities.Actions
                .Select(action => new ProjectStructureRuntimeNodeActionDescriptor(
                    action.ActionId,
                    action.Label,
                    action.Surface,
                    action.Description))
                .ToList(),
            capabilities.Guidance);
    }

    private static ProjectObjectMediaPayload? MapMedia(ProjectStructureRuntimeMediaPayload? media)
        => media is null
            ? null
            : new ProjectObjectMediaPayload(media.FileName, media.ContentType, media.Base64Data);

    private async Task<ProjectObjectMediaPayload> ResolveAssetCreateMediaAsync(
        ProjectStructureRuntimeAssetCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Media is not null)
        {
            EnsureValidMediaPayload(request.Media);
            return MapMedia(request.Media)!;
        }

        if (string.IsNullOrWhiteSpace(request.SourceWorkspacePath))
        {
            throw new ProjectStructureAgentException(
                400,
                "MediaSourceRequired",
                "Asset creation requires either a media payload or a source workspace path.");
        }

        var resolution = sourceWorkspacePathResolver.ResolveExistingFile(request.SourceWorkspacePath);
        var bytes = await File.ReadAllBytesAsync(resolution.FullPath, cancellationToken);
        var fileName = ResolveSourceAssetFileName(request.SourceFileName, resolution.FullPath);
        var contentType = ResolveSourceAssetContentType(request.SourceContentType, fileName);
        return new ProjectObjectMediaPayload(
            fileName,
            contentType,
            Convert.ToBase64String(bytes));
    }

    private static void EnsureValidMediaPayload(ProjectStructureRuntimeMediaPayload media)
    {
        if (string.IsNullOrWhiteSpace(media.FileName))
        {
            throw new ProjectStructureAgentException(400, "FileNameRequired", "Uploaded media requires a file name.");
        }

        if (string.IsNullOrWhiteSpace(media.Base64Data))
        {
            throw new ProjectStructureAgentException(400, "MediaPayloadRequired", "Uploaded media requires base64 content.");
        }

        try
        {
            _ = Convert.FromBase64String(media.Base64Data.Trim());
        }
        catch (FormatException ex)
        {
            throw new ProjectStructureAgentException(400, "InvalidBase64Payload", "Uploaded media content was not valid base64.", ex.Message);
        }
    }

    private static string ResolveSourceAssetFileName(string? requestedFileName, string fullPath)
    {
        var candidate = string.IsNullOrWhiteSpace(requestedFileName)
            ? Path.GetFileName(fullPath)
            : Path.GetFileName(requestedFileName.Trim());
        return string.IsNullOrWhiteSpace(candidate)
            ? "project-asset.bin"
            : candidate;
    }

    private static string ResolveSourceAssetContentType(string? requestedContentType, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(requestedContentType))
        {
            return requestedContentType.Trim();
        }

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".json" => "application/json",
            ".md" => "text/markdown",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private static ProjectStructureAgentContext MapAgent(ProjectStructureRuntimeAgentContext agent)
        => new(
            agent.AgentId,
            agent.AgentName,
            agent.MachineName,
            agent.RepositoryRoot,
            agent.BranchName,
            agent.SessionId);

    private static ProjectStructureRuntimeProjectStatus MapProjectStatus(ProjectStatus status)
        => status switch
        {
            ProjectStatus.Draft => ProjectStructureRuntimeProjectStatus.Draft,
            ProjectStatus.Active => ProjectStructureRuntimeProjectStatus.Active,
            ProjectStatus.OnHold => ProjectStructureRuntimeProjectStatus.OnHold,
            ProjectStatus.Completed => ProjectStructureRuntimeProjectStatus.Completed,
            ProjectStatus.Archived => ProjectStructureRuntimeProjectStatus.Archived,
            _ => throw new InvalidOperationException($"Project status '{status}' is not supported by the project-structure runtime gateway.")
        };

    private static ProjectStructureProjectRole MapProjectRole(ProjectStructureRuntimeProjectRole role)
        => role switch
        {
            ProjectStructureRuntimeProjectRole.None => ProjectStructureProjectRole.None,
            ProjectStructureRuntimeProjectRole.ActiveProject => ProjectStructureProjectRole.ActiveProject,
            ProjectStructureRuntimeProjectRole.Subproject => ProjectStructureProjectRole.Subproject,
            ProjectStructureRuntimeProjectRole.ParentProject => ProjectStructureProjectRole.ParentProject,
            ProjectStructureRuntimeProjectRole.AdditionalParentProject => ProjectStructureProjectRole.AdditionalParentProject,
            _ => throw new InvalidOperationException($"Project role '{role}' is not supported by the project-structure runtime gateway.")
        };

    private static ProjectStructureRuntimeProjectRole MapProjectRole(ProjectStructureProjectRole role)
        => role switch
        {
            ProjectStructureProjectRole.None => ProjectStructureRuntimeProjectRole.None,
            ProjectStructureProjectRole.ActiveProject => ProjectStructureRuntimeProjectRole.ActiveProject,
            ProjectStructureProjectRole.Subproject => ProjectStructureRuntimeProjectRole.Subproject,
            ProjectStructureProjectRole.ParentProject => ProjectStructureRuntimeProjectRole.ParentProject,
            ProjectStructureProjectRole.AdditionalParentProject => ProjectStructureRuntimeProjectRole.AdditionalParentProject,
            _ => throw new InvalidOperationException($"Project role '{role}' is not supported by the project-structure runtime gateway.")
        };

    private static HashSet<string>? ResolveIncludedNodeIds(
        IReadOnlyList<ProjectStructureNode> nodes,
        ProjectStructureRuntimeReadRequest request)
    {
        var selectedIds = new HashSet<string>(StringComparer.Ordinal);
        if (request.NodeIds is not null)
        {
            foreach (var nodeId in request.NodeIds.Where(nodeId => !string.IsNullOrWhiteSpace(nodeId)))
            {
                selectedIds.Add(nodeId.Trim());
            }
        }

        if (request.SubtreeRootIds is not null)
        {
            var childrenByParent = nodes
                .Where(node => !string.IsNullOrWhiteSpace(node.ParentId))
                .GroupBy(node => node.ParentId!, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(item => item.Id).ToList(),
                    StringComparer.Ordinal);

            foreach (var rootId in request.SubtreeRootIds.Where(rootId => !string.IsNullOrWhiteSpace(rootId)))
            {
                ExpandSubtree(rootId.Trim(), childrenByParent, selectedIds);
            }
        }

        return selectedIds.Count == 0 ? null : selectedIds;
    }

    private static void ExpandSubtree(
        string rootId,
        IReadOnlyDictionary<string, List<string>> childrenByParent,
        ISet<string> selectedIds)
    {
        var queue = new Queue<string>();
        queue.Enqueue(rootId);
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (!selectedIds.Add(currentId))
            {
                continue;
            }

            if (!childrenByParent.TryGetValue(currentId, out var childIds))
            {
                continue;
            }

            foreach (var childId in childIds)
            {
                queue.Enqueue(childId);
            }
        }
    }
}
