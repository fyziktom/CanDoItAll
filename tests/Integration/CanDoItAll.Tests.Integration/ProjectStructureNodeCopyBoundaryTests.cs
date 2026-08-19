using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectStructureNodeCopyBoundaryTests
{
    private static readonly JsonSerializerOptions FunctionResultJsonOptions = CreateFunctionResultJsonOptions();

    [Fact]
    public async Task Provider_tool_copies_one_exact_forest_with_stable_mapping_and_managed_asset_content()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projects, "Runtime node-copy boundary");
        var fixture = await CreateCopyFixtureAsync(
            workbench,
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>(),
            projectId);
        var capture = new ProjectStructureCanonicalGraphSnapshotCapture(
            scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>(),
            projects,
            scope.ServiceProvider.GetRequiredService<IWorkspacePathAccessGuard>());
        var before = await capture.CaptureAsync(projectId);
        var tools = await CreateToolsAsync(scope.ServiceProvider, projectId);
        var copyTool = Assert.IsAssignableFrom<AIFunction>(
            Assert.Single(tools, tool => tool.Name == AgentToolInvocationPolicyMetadata.ProjectStructureNodesCopy));
        var requiredRequestProperties = copyTool.JsonSchema
            .GetProperty("properties")
            .GetProperty("request")
            .GetProperty("required")
            .EnumerateArray()
            .Select(property => property.GetString())
            .ToArray();

        Assert.Contains("sourceNodeIds", requiredRequestProperties);
        Assert.Contains("destinationParentNodeId", requiredRequestProperties);
        Assert.Equal(
            ToolInvocationClassification.Mutation,
            AgentToolInvocationPolicyMetadata.Classify(copyTool.Name));
        Assert.True(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(copyTool.Name));

        var missingSource = await Assert.ThrowsAsync<ProjectStructureAgentException>(
            () => InvokeAsync<ProjectStructureNodesCopyResult>(
                copyTool,
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["request"] = new ProjectStructureNodesCopyInput([], fixture.Destination.Id)
                }));
        var missingDestination = await Assert.ThrowsAsync<ProjectStructureAgentException>(
            () => InvokeAsync<ProjectStructureNodesCopyResult>(
                copyTool,
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["request"] = new ProjectStructureNodesCopyInput([fixture.SourceRoot.Id], " ")
                }));
        Assert.Equal("NodeCopySourceRequired", missingSource.ErrorCode);
        Assert.Equal("NodeCopyDestinationRequired", missingDestination.ErrorCode);
        var afterRejectedInputs = await capture.CaptureAsync(projectId);
        Assert.Equal(before.Nodes, afterRejectedInputs.Nodes);
        Assert.Equal(before.Links, afterRejectedInputs.Links);
        Assert.Equal(before.ManagedAssets, afterRejectedInputs.ManagedAssets);
        Assert.Equal(before.HierarchyEdges, afterRejectedInputs.HierarchyEdges);

        var result = await InvokeAsync<ProjectStructureNodesCopyResult>(
            copyTool,
            new AIFunctionArguments
            {
                ["projectId"] = projectId,
                ["request"] = new ProjectStructureNodesCopyInput(
                    [fixture.SourceRoot.Id, fixture.SourceAsset.Id],
                    fixture.Destination.Id)
            });
        var after = await capture.CaptureAsync(projectId);

        Assert.Equal(projectId, result.ProjectId);
        Assert.Equal(fixture.Destination.Id, result.DestinationParentNodeId);
        Assert.Equal([fixture.SourceRoot.Id], result.SourceRootNodeIds);
        Assert.Single(result.CopiedRootNodeIds);
        Assert.Equal(2, result.CopiedNodeCount);
        Assert.Equal(
            result.NodeMappings.OrderBy(mapping => mapping.SourceNodeId, StringComparer.Ordinal),
            result.NodeMappings);
        Assert.Equal(fixture.ExpectedOmittedBoundaryLinks, result.OmittedBoundaryLinks);

        var copiedRootId = Assert.Single(
            result.NodeMappings,
            mapping => mapping.SourceNodeId == fixture.SourceRoot.Id).CopiedNodeId;
        var copiedAssetId = Assert.Single(
            result.NodeMappings,
            mapping => mapping.SourceNodeId == fixture.SourceAsset.Id).CopiedNodeId;
        Assert.Equal(copiedRootId, Assert.Single(result.CopiedRootNodeIds));
        Assert.Equal(
            fixture.Destination.Id,
            Assert.Single(after.Nodes, node => node.Id == copiedRootId).ParentId);
        Assert.Equal(
            copiedRootId,
            Assert.Single(after.Nodes, node => node.Id == copiedAssetId).ParentId);

        var sourceAsset = Assert.Single(before.ManagedAssets, asset => asset.NodeId == fixture.SourceAsset.Id);
        var copiedAsset = Assert.Single(after.ManagedAssets, asset => asset.NodeId == copiedAssetId);
        Assert.Equal(sourceAsset.MediaRelativePath, copiedAsset.MediaRelativePath);
        Assert.Equal(sourceAsset.MediaContentType, copiedAsset.MediaContentType);
        Assert.Equal(sourceAsset.MediaOriginalFileName, copiedAsset.MediaOriginalFileName);
        Assert.Equal(sourceAsset.ContentLength, copiedAsset.ContentLength);
        Assert.Equal(sourceAsset.Sha256, copiedAsset.Sha256);
        Assert.Contains(after.Links, link =>
            link.SourceId == copiedRootId &&
            link.TargetId == copiedAssetId &&
            link.Kind == ProjectObjectLinkKind.Uses &&
            link.IsUserAuthored);
        Assert.DoesNotContain(after.Links, link =>
            link.SourceId == copiedRootId &&
            link.TargetId == fixture.External.Id &&
            link.Kind == ProjectObjectLinkKind.DependsOn);

        var copiedNodeIds = result.NodeMappings
            .Select(mapping => mapping.CopiedNodeId)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            before.Nodes,
            after.Nodes.Where(node => !copiedNodeIds.Contains(node.Id)).ToArray());
        Assert.Equal(
            copiedNodeIds.OrderBy(nodeId => nodeId, StringComparer.Ordinal),
            after.Nodes
                .Where(node => copiedNodeIds.Contains(node.Id))
                .Select(node => node.Id)
                .OrderBy(nodeId => nodeId, StringComparer.Ordinal));

        var sourceRoot = Assert.Single(before.Nodes, node => node.Id == fixture.SourceRoot.Id);
        var sourceAssetNode = Assert.Single(before.Nodes, node => node.Id == fixture.SourceAsset.Id);
        var copiedRoot = Assert.Single(after.Nodes, node => node.Id == copiedRootId);
        var copiedAssetNode = Assert.Single(after.Nodes, node => node.Id == copiedAssetId);
        Assert.Equal(
            sourceRoot with
            {
                Id = copiedRootId,
                ParentId = fixture.Destination.Id,
                X = copiedRoot.X,
                Y = copiedRoot.Y
            },
            copiedRoot);
        Assert.Equal(
            sourceAssetNode with
            {
                Id = copiedAssetId,
                ParentId = copiedRootId,
                X = copiedAssetNode.X,
                Y = copiedAssetNode.Y
            },
            copiedAssetNode);
        Assert.Equal(copiedRoot.X - sourceRoot.X, copiedAssetNode.X - sourceAssetNode.X);
        Assert.Equal(copiedRoot.Y - sourceRoot.Y, copiedAssetNode.Y - sourceAssetNode.Y);

        var unchangedLinks = after.Links
            .Where(link =>
                !copiedNodeIds.Contains(link.SourceId) &&
                !copiedNodeIds.Contains(link.TargetId))
            .ToArray();
        Assert.Equal(before.Links, unchangedLinks);
        Assert.Equal(
            new ProjectStructureCanonicalLinkSnapshot[]
            {
                new(fixture.Destination.Id, copiedRootId, ProjectObjectLinkKind.BelongsTo, false),
                new(copiedRootId, copiedAssetId, ProjectObjectLinkKind.BelongsTo, false),
                new(copiedRootId, copiedAssetId, ProjectObjectLinkKind.Uses, true)
            }
                .OrderBy(link => link.SourceId, StringComparer.Ordinal)
                .ThenBy(link => link.TargetId, StringComparer.Ordinal)
                .ThenBy(link => link.Kind)
                .ThenBy(link => link.IsUserAuthored),
            after.Links
                .Where(link =>
                    copiedNodeIds.Contains(link.SourceId) ||
                    copiedNodeIds.Contains(link.TargetId))
                .OrderBy(link => link.SourceId, StringComparer.Ordinal)
                .ThenBy(link => link.TargetId, StringComparer.Ordinal)
                .ThenBy(link => link.Kind)
                .ThenBy(link => link.IsUserAuthored));

        Assert.Equal(
            before.ManagedAssets,
            after.ManagedAssets.Where(asset => !copiedNodeIds.Contains(asset.NodeId)).ToArray());
        Assert.Equal(sourceAsset with { NodeId = copiedAssetId }, copiedAsset);
        Assert.Equal(before.HierarchyEdges, after.HierarchyEdges);
    }

    [Fact]
    public async Task Non_task_provider_authority_rejects_task_in_selected_subtree_with_zero_delta()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projects, "Non-task node-copy authority");
        var projectRootId = $"project:{projectId:D}";
        var destination = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Non-task copy destination",
                string.Empty,
                string.Empty,
                projectRootId));
        var source = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Task-bearing source",
                string.Empty,
                string.Empty,
                projectRootId));
        var canonicalTask = await CreateCanonicalTaskChildAsync(
            workbench,
            projectId,
            source.Id);
        var capture = new ProjectStructureCanonicalGraphSnapshotCapture(
            scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>(),
            projects,
            scope.ServiceProvider.GetRequiredService<IWorkspacePathAccessGuard>());
        var before = await capture.CaptureAsync(projectId);
        var tools = await CreateToolsAsync(scope.ServiceProvider, projectId);
        var copyTool = Assert.IsAssignableFrom<AIFunction>(
            Assert.Single(tools, tool => tool.Name == AgentToolInvocationPolicyMetadata.ProjectStructureNodesCopy));

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(
            () => InvokeAsync<ProjectStructureNodesCopyResult>(
                copyTool,
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["request"] = new ProjectStructureNodesCopyInput(
                        [source.Id],
                        destination.Id)
                }));

        Assert.Equal("NodeCopyTaskAuthorityRequired", exception.ErrorCode);
        Assert.Contains(canonicalTask.Id, exception.Message, StringComparison.Ordinal);
        var after = await capture.CaptureAsync(projectId);
        Assert.Equal(before.Nodes, after.Nodes);
        Assert.Equal(before.Links, after.Links);
        Assert.Equal(before.ManagedAssets, after.ManagedAssets);
        Assert.Equal(before.HierarchyEdges, after.HierarchyEdges);
    }

    [Fact]
    public async Task Http_api_copy_route_returns_exact_mapping_and_copied_asset_bytes()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
            "project-structure-node-copy-http-boundary",
            environment => environment.CreatePostgreSqlProfile("node-copy-http-boundary"));
        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                "HTTP node-copy boundary",
                "Expose the existing UI copy engine through the agent API.",
                "Prove exact subtree and asset-content copying.",
                "Validation",
                ProjectStatus.Active));

        CopyFixture fixture;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            fixture = await CreateCopyFixtureAsync(
                scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>(),
                scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>(),
                project.Id);
        }

        var result = await PostAndReadAsync<ProjectStructureNodesCopyResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id:D}/nodes/copy",
            new ProjectStructureNodesCopyInput(
                [fixture.SourceRoot.Id],
                fixture.Destination.Id));
        var copiedAssetId = Assert.Single(
            result.NodeMappings,
            mapping => mapping.SourceNodeId == fixture.SourceAsset.Id).CopiedNodeId;
        var content = await host.Client.GetFromJsonAsync<ProjectStructureAssetContentDescriptor>(
            $"/api/project-structure/projects/{project.Id:D}/assets/{copiedAssetId}/content",
            ProjectStructureHttpContractTestJson.SerializerOptions);

        Assert.NotNull(content);
        Assert.Equal(2, result.CopiedNodeCount);
        Assert.Equal(fixture.Destination.Id, result.DestinationParentNodeId);
        Assert.Equal(fixture.ExpectedOmittedBoundaryLinks, result.OmittedBoundaryLinks);
        Assert.Equal("# Copy proof\nExact managed content.", Encoding.UTF8.GetString(
            Convert.FromBase64String(content.Base64Data)));

        await using var rejectionScope = host.App.Services.CreateAsyncScope();
        var workbench = rejectionScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var taskSource = await workbench.CreateObjectAsync(
            project.Id,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "HTTP task-bearing copy source",
                string.Empty,
                string.Empty,
                $"project:{project.Id:D}"));
        var canonicalTask = await CreateCanonicalTaskChildAsync(
            workbench,
            project.Id,
            taskSource.Id);
        var capture = new ProjectStructureCanonicalGraphSnapshotCapture(
            rejectionScope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>(),
            rejectionScope.ServiceProvider.GetRequiredService<ProjectsService>(),
            rejectionScope.ServiceProvider.GetRequiredService<IWorkspacePathAccessGuard>());
        var beforeRejectedCopy = await capture.CaptureAsync(project.Id);

        using var rejectedCopyResponse = await host.Client.PostAsJsonAsync(
            $"/api/project-structure/projects/{project.Id:D}/nodes/copy",
            new ProjectStructureNodesCopyInput(
                [taskSource.Id],
                fixture.Destination.Id));
        var rejectedCopyBody = await rejectedCopyResponse.Content.ReadFromJsonAsync<ProjectStructureCopyApiError>();

        Assert.Equal(System.Net.HttpStatusCode.Conflict, rejectedCopyResponse.StatusCode);
        Assert.NotNull(rejectedCopyBody);
        Assert.Equal("NodeCopyTaskAuthorityRequired", rejectedCopyBody.Error.ErrorCode);
        Assert.Contains(canonicalTask.Id, rejectedCopyBody.Error.Message, StringComparison.Ordinal);
        var afterRejectedCopy = await capture.CaptureAsync(project.Id);
        Assert.Equal(beforeRejectedCopy.Nodes, afterRejectedCopy.Nodes);
        Assert.Equal(beforeRejectedCopy.Links, afterRejectedCopy.Links);
        Assert.Equal(beforeRejectedCopy.ManagedAssets, afterRejectedCopy.ManagedAssets);
        Assert.Equal(beforeRejectedCopy.HierarchyEdges, afterRejectedCopy.HierarchyEdges);
    }

    private static async Task<CopyFixture> CreateCopyFixtureAsync(
        ProjectWorkbenchService workbench,
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid projectId)
    {
        var projectRootId = $"project:{projectId:D}";
        var destination = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Copy destination",
                "Explicit destination parent",
                "Copied roots must land here.",
                projectRootId,
                ObjectSubtype: "planning"));
        var external = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "External boundary node",
                "Must not be copied or linked",
                "Cross-forest links are intentionally omitted.",
                projectRootId));
        var sourceRoot = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Copy source",
                "Selected source root",
                "The complete editable subtree is copied once.",
                projectRootId,
                ObjectSubtype: "architecture"));
        var sourceAsset = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Copy proof",
                "Managed Markdown child",
                "Content must remain byte-identical.",
                sourceRoot.Id,
                ObjectSubtype: "md",
                Media: new ProjectObjectMediaPayload(
                    "copy-proof.md",
                    "text/markdown",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes("# Copy proof\nExact managed content.")))));
        await workbench.LinkObjectsAsync(
            projectId,
            sourceRoot.Id,
            sourceAsset.Id,
            ProjectObjectLinkKind.Uses);
        await workbench.LinkObjectsAsync(
            projectId,
            sourceRoot.Id,
            external.Id,
            ProjectObjectLinkKind.DependsOn);
        await workbench.LinkObjectsAsync(
            projectId,
            external.Id,
            sourceAsset.Id,
            ProjectObjectLinkKind.Blocks);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedBoundaryLinks = await dbContext.Set<ProjectObjectLinkRecord>()
            .AsNoTracking()
            .Where(link =>
                link.ProjectId == projectId &&
                !link.IsSystemManaged &&
                ((link.SourceNodeKey == sourceRoot.Id &&
                  link.TargetNodeKey == external.Id &&
                  link.LinkKind == ProjectObjectLinkKind.DependsOn) ||
                 (link.SourceNodeKey == external.Id &&
                  link.TargetNodeKey == sourceAsset.Id &&
                  link.LinkKind == ProjectObjectLinkKind.Blocks)))
            .ToArrayAsync();
        var expectedOmittedBoundaryLinks = persistedBoundaryLinks
            .OrderBy(link => link.SourceNodeKey, StringComparer.Ordinal)
            .ThenBy(link => link.TargetNodeKey, StringComparer.Ordinal)
            .ThenBy(link => link.LinkKind)
            .ThenBy(link => link.Id)
            .Select(link => new ProjectStructureCopyOmittedLink(
                link.Id,
                link.SourceNodeKey,
                link.TargetNodeKey,
                link.LinkKind))
            .ToArray();
        Assert.Equal(2, expectedOmittedBoundaryLinks.Length);
        return new CopyFixture(
            destination,
            external,
            sourceRoot,
            sourceAsset,
            expectedOmittedBoundaryLinks);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projects, string name)
    {
        var result = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = "Isolated project for node-copy boundary coverage.",
            Objective = "Prove exact agent-visible copy semantics.",
            CurrentPhase = "Validation"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static Task<ProjectStructureNode> CreateCanonicalTaskChildAsync(
        ProjectWorkbenchService workbench,
        Guid projectId,
        string parentNodeId)
    {
        return workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Canonical task child",
                string.Empty,
                string.Empty,
                parentNodeId,
                ObjectSubtype: ProjectObjectSubtypePolicy.Task,
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(
                    new ProjectObjectMetadataEnvelope
                    {
                        WorkItem = new ProjectWorkItemMetadata
                        {
                            WorkItemKind = ProjectWorkItemKind.Task,
                            ExecutionState = ProjectTaskExecutionState.NotStarted
                        }
                    })));
    }

    private static async Task<IReadOnlyList<AITool>> CreateToolsAsync(
        IServiceProvider services,
        Guid projectId)
    {
        var provider = services
            .GetServices<IAgentRuntimeToolProvider>()
            .OfType<ProjectStructureAgentRuntimeToolProvider>()
            .Single();
        var agent = CreateAgent(projectId);
        var providerProfile = new ProviderProfile(
            agent.ProviderProfileId!.Value,
            "Node-copy integration provider",
            ProviderKind.OpenAi,
            "https://api.openai.com",
            "OPENAI_API_KEY",
            agent.Model,
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: string.Empty,
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: []);
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = "project-structure",
            SourceId = projectId.ToString("D")
        };

        return await provider.CreateToolsAsync(
            new AgentRuntimeToolProviderContext(
                agent,
                providerProfile,
                [],
                SuppressApprovalRequirements: false,
                AgentRuntimeToolProviderPurpose.InteractiveChat,
                RuntimeSessionKey: $"project-structure-copy:{projectId:D}",
                intent,
                Tags: new Dictionary<string, string>()),
            CancellationToken.None);
    }

    private static AgentDefinition CreateAgent(Guid projectId)
    {
        var now = DateTimeOffset.UtcNow;
        var configurationJson = AgentProjectStructureAccessMetadata.Write(
            "{}",
            new AgentProjectStructureAccessSettings
            {
                CanRead = true,
                CanWriteNonTaskStructure = true,
                AllowedProjectIds = [projectId]
            });
        return new AgentDefinition(
            Guid.NewGuid(),
            "Node Copy Integration Agent",
            "Project architect",
            "Exercises the governed project-structure copy boundary.",
            "Copy only the explicit selected forest under the explicit destination.",
            AgentLifecycleStatus.Active,
            Guid.NewGuid(),
            "gpt-5-mini",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.ProviderDefault,
            0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            configurationJson,
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            [],
            [],
            now,
            now);
    }

    private static async Task<T> InvokeAsync<T>(AITool tool, AIFunctionArguments arguments)
    {
        var function = Assert.IsAssignableFrom<AIFunction>(tool);
        var rawResult = await function.InvokeAsync(arguments);
        return rawResult switch
        {
            T result => result,
            JsonElement json => JsonSerializer.Deserialize<T>(json.GetRawText(), FunctionResultJsonOptions)
                ?? throw new InvalidOperationException($"Tool '{tool.Name}' returned null JSON."),
            _ => throw new InvalidOperationException(
                $"Tool '{tool.Name}' returned unexpected result type '{rawResult?.GetType().FullName ?? "<null>"}'.")
        };
    }

    private static async Task<T> PostAndReadAsync<T>(HttpClient client, string path, object request)
    {
        var response = await client.PostAsJsonAsync(path, request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {body}");
        }

        return await response.Content.ReadFromJsonAsync<T>(
                ProjectStructureHttpContractTestJson.SerializerOptions)
            ?? throw new InvalidOperationException($"No {typeof(T).Name} payload was returned.");
    }

    private static JsonSerializerOptions CreateFunctionResultJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record CopyFixture(
        ProjectStructureNode Destination,
        ProjectStructureNode External,
        ProjectStructureNode SourceRoot,
        ProjectStructureNode SourceAsset,
        IReadOnlyList<ProjectStructureCopyOmittedLink> ExpectedOmittedBoundaryLinks);

    private sealed record ProjectStructureCopyApiError(ProjectStructureCopyApiErrorDetail Error);

    private sealed record ProjectStructureCopyApiErrorDetail(string ErrorCode, string Message);
}
