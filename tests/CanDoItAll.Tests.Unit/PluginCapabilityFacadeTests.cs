using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class PluginCapabilityFacadeTests
{
    [Fact]
    public void WorkspaceFile_plugin_facade_rejects_absolute_path_escape()
    {
        using var temp = new TempDirectory();
        var outsidePath = Path.Combine(
            Path.GetDirectoryName(temp.Path) ?? Path.GetTempPath(),
            $"outside-{Guid.NewGuid():N}.txt");
        File.WriteAllText(outsidePath, "outside");

        try
        {
            var facade = new PluginWorkspaceFiles(new WorkspaceFileService(temp.Path));

            var exception = Assert.Throws<InvalidOperationException>(() => facade.ReadTextFile(outsidePath));

            Assert.Contains("workspace-relative", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    [Fact]
    public void WorkspaceFile_plugin_facade_rejects_oversized_read_limits_before_io()
    {
        using var temp = new TempDirectory();
        var facade = new PluginWorkspaceFiles(new WorkspaceFileService(temp.Path));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            facade.ReadTextFile("missing.txt", PluginWorkspaceFileLimits.MaxReadCharacters + 1));

        Assert.Equal("maxCharacters", exception.ParamName);
    }

    [Fact]
    public async Task ProjectStructure_executor_uses_runtime_gateway_for_task_creation()
    {
        var projectId = Guid.NewGuid();
        var gateway = new RecordingProjectStructureGateway();
        var payloadJson = """
            {
              "tasks": [
                {
                  "title": "Review plugin boundary",
                  "summary": "Check architecture before plugin loading.",
                  "owner": "Architecture",
                  "dueUtc": "2026-05-13T13:00:00Z"
                }
              ],
              "runContext": {
                "agentId": "agent-1",
                "agentName": "Plugin worker",
                "workflowNodeId": "parent-node"
              }
            }
            """;

        var result = await ExecuteProjectStructureAsync(
            gateway,
            new WorkflowProjectStructureExecutorSettings
            {
                Operation = WorkflowProjectStructureOperation.CreateTaskNodes,
                ProjectId = projectId,
                TaskItemsJsonPath = "$.tasks",
                MaxTaskNodes = 2
            },
            payloadJson);

        Assert.Contains("\"createdTaskCount\":1", result.PayloadJson, StringComparison.Ordinal);
        var created = Assert.Single(gateway.CreatedNodes);
        Assert.Equal(projectId, created.ProjectId);
        Assert.Equal(ProjectObjectType.WorkItem, created.Request.ObjectType);
        Assert.Equal("Review plugin boundary", created.Request.Title);
        Assert.Equal("parent-node", created.Request.ParentNodeKey);
        Assert.Equal("agent-1", created.Agent.AgentId);
    }

    [Fact]
    public async Task ProjectStructure_executor_reports_missing_runtime_gateway()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ExecuteProjectStructureAsync(
                new UnavailableProjectStructureRuntimeGateway(),
                new WorkflowProjectStructureExecutorSettings
                {
                    Operation = WorkflowProjectStructureOperation.ListProjects
                }));

        Assert.Contains("IProjectStructureRuntimeGateway", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginCapability_storage_gateway_contract_does_not_expose_driver_or_catalog_types()
    {
        var constructorParameters = typeof(PluginStorageGateway)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        var resultProperties = typeof(PluginStoragePlacementResult)
            .GetProperties()
            .Select(property => property.PropertyType)
            .ToArray();

        Assert.DoesNotContain(typeof(IStorageDriverRegistry), constructorParameters);
        Assert.DoesNotContain(typeof(IStorageDriver), resultProperties);
        Assert.DoesNotContain(typeof(StorageCatalogRecord), resultProperties);
    }

    [Fact]
    public async Task PluginCapability_storage_gateway_delegates_through_safe_services()
    {
        var accessService = new RecordingStorageAccessService();
        var placementService = new RecordingStoragePlacementService();
        var gateway = new PluginStorageGateway(accessService, placementService);
        var storageId = Guid.NewGuid();

        var placement = await gateway.PlaceAsync(new PluginStoragePlacementRequest(
            "proof.txt",
            "text/plain",
            "proof"u8.ToArray(),
            StorageUsagePurpose.ProjectAsset,
            StorageContentKind.Text,
            ProjectId: Guid.NewGuid(),
            NodeKey: "node-1",
            RelativePathHint: "managed-files/proof/proof.txt",
            PreviewRequired: true,
            PreferredStorageId: storageId));
        var descriptor = await gateway.DescribeAsync(placement.Reference);

        Assert.Equal(storageId, placementService.Request?.PreferredStorageId);
        Assert.Equal("proof.txt", placementService.Request?.FileName);
        Assert.Equal("proof.txt", descriptor.DisplayFileName);
        Assert.Equal(placement.Reference, accessService.Reference);
        Assert.Equal(storageId, placement.StorageId);
        Assert.Equal(StorageProviderKind.FileSystem, placement.ProviderKind);
    }

    private static async Task<WorkflowNodeExecutionResult> ExecuteProjectStructureAsync(
        IProjectStructureRuntimeGateway gateway,
        WorkflowProjectStructureExecutorSettings settings,
        string inputJson = "{}")
    {
        var executor = new ProjectStructureWorkflowExecutor(gateway);
        var node = new WorkflowNode(
            new WorkflowNodeId("project-structure"),
            WorkflowNodeKind.Executor,
            "Project structure",
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"))
            {
                ExecutorId = BuiltInWorkflowExecutorDescriptors.ProjectStructure.Id,
                ExecutorSettingsJson = Serialize(settings),
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            });
        var definition = new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Project-structure executor test",
            "Project-structure executor test.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(node.Id, [node], []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var context = new WorkflowExecutorExecutionContext(
            definition,
            node,
            executor.Descriptor,
            node.Settings.ExecutorSettingsJson,
            WorkflowExecutorExecutionPolicy.Default);

        return await executor.ExecuteAsync(context, new WorkflowNodeInput(inputJson));
    }

    private static string Serialize<T>(T value)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Serialize(value, options);
    }

    private sealed class RecordingProjectStructureGateway : IProjectStructureRuntimeGateway
    {
        public List<ProjectStructureNodeCreateCall> CreatedNodes { get; } = [];

        public Task<IReadOnlyList<ProjectStructureRuntimeProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectStructureRuntimeProjectSummary>>([]);

        public Task<ProjectStructureRuntimeReadResponse> ReadStructureAsync(
            Guid projectId,
            ProjectStructureRuntimeReadRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ProjectStructureRuntimeReadResponse(projectId, "Project", [], [], []));

        public Task<ProjectStructureRuntimeNodeSummary> CreateNodeAsync(
            Guid projectId,
            ProjectStructureRuntimeNodeCreateRequest request,
            ProjectStructureRuntimeAgentContext agent,
            CancellationToken cancellationToken = default)
        {
            CreatedNodes.Add(new ProjectStructureNodeCreateCall(projectId, request, agent));
            return Task.FromResult(new ProjectStructureRuntimeNodeSummary(
                $"node-{CreatedNodes.Count}",
                request.ParentNodeKey,
                request.ObjectType,
                request.ObjectSubtype ?? string.Empty,
                request.Title,
                request.Subtitle,
                "new",
                request.Notes,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                null,
                [],
                "none",
                0,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                0,
                request.StartUtc,
                request.EndUtc,
                request.MetadataJson,
                ProjectStructureRuntimeProjectRole.ActiveProject,
                null,
                0,
                request.X,
                request.Y,
                request.DurationSeconds));
        }

        public Task<ProjectStructureRuntimeNodeSummary> CreateAssetAsync(
            Guid projectId,
            ProjectStructureRuntimeAssetCreateRequest request,
            ProjectStructureRuntimeAgentContext agent,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed record ProjectStructureNodeCreateCall(
        Guid ProjectId,
        ProjectStructureRuntimeNodeCreateRequest Request,
        ProjectStructureRuntimeAgentContext Agent);

    private sealed class RecordingStorageAccessService : IStorageAccessService
    {
        public StorageObjectReference? Reference { get; private set; }

        public Task<StorageAccessDescriptor> DescribeAsync(
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
        {
            Reference = reference;
            return Task.FromResult(new StorageAccessDescriptor(
                "/storage/objects/preview?ref=test",
                "/storage/objects/download?ref=test",
                null,
                true,
                true,
                false,
                reference.DisplayName,
                reference.ContentType,
                reference.ContentLength,
                string.Empty));
        }
    }

    private sealed class RecordingStoragePlacementService : IStoragePlacementService
    {
        public StoragePlacementRequest? Request { get; private set; }

        public Task<StoragePlacementResult> PlaceAsync(
            StoragePlacementRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            var storage = new StorageCatalogRecord
            {
                Id = request.PreferredStorageId ?? Guid.NewGuid(),
                Name = "Workspace files",
                ProviderKind = StorageProviderKind.FileSystem,
                CapabilityMask = StorageCapability.Read | StorageCapability.Write | StorageCapability.Download | StorageCapability.InlinePreview
            };
            var reference = new StorageObjectReference(
                storage.Id,
                storage.ProviderKind,
                StorageLocatorKind.RelativePath,
                request.RelativePathHint ?? request.FileName,
                request.FileName,
                request.ContentType,
                request.Content.LongLength);
            var access = new StorageAccessDescriptor(
                "/storage/objects/preview?ref=test",
                "/storage/objects/download?ref=test",
                null,
                true,
                true,
                false,
                request.FileName,
                request.ContentType,
                request.Content.LongLength,
                string.Empty);
            var recommendation = new StorageRecommendation(
                new StorageRecommendationCandidate(
                    storage.Id,
                    storage.Name,
                    storage.ProviderKind,
                    storage.CapabilityMask,
                    StorageHealthStatus.Healthy,
                    false,
                    "Unit test."),
                [],
                "Unit test.",
                []);

            return Task.FromResult(new StoragePlacementResult(
                storage,
                recommendation,
                new StorageWriteResult(reference, access),
                access.PreviewUrl,
                Path.Combine(Path.GetTempPath(), request.FileName),
                request.RelativePathHint ?? request.FileName));
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"candoitall-plugin-capability-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
