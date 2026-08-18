using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectStructureAgentRuntimeToolRoundTripIntegrationTests
{
    private static readonly JsonSerializerOptions FunctionResultJsonOptions = CreateFunctionResultJsonOptions();

    private static readonly string[] ExplicitLeaseToolNames =
    [
        AgentToolInvocationPolicyMetadata.ProjectStructureProjectLeaseAcquire,
        AgentToolInvocationPolicyMetadata.ProjectStructureRepoBranchLeaseAcquire,
        AgentToolInvocationPolicyMetadata.ProjectStructureLeaseGet,
        AgentToolInvocationPolicyMetadata.ProjectStructureLeaseRenew,
        AgentToolInvocationPolicyMetadata.ProjectStructureLeaseRelease
    ];

    [Fact]
    public async Task Interactive_tools_use_automatic_mutation_leases_without_exposing_explicit_lease_tokens()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();
        var projectId = await CreateProjectAsync(projects);

        var tools = await CreateToolsAsync(scope.ServiceProvider, projectId);

        Assert.DoesNotContain(
            tools,
            tool => ExplicitLeaseToolNames.Contains(tool.Name, StringComparer.Ordinal));
        var asset = await InvokeAsync<ProjectStructureNodeSummary>(
            FindTool(tools, AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate),
            new AIFunctionArguments
            {
                ["projectId"] = projectId,
                ["request"] = new ProjectStructureAgentAssetCreateInput(
                    ProjectObjectType.File,
                    "Interactive automatic lease proof",
                    "Agent-created text asset",
                    "Created without exposing or supplying an explicit lease token.",
                    new ProjectObjectMediaPayload(
                        "interactive-automatic-lease.txt",
                        "text/plain",
                        Convert.ToBase64String(Encoding.UTF8.GetBytes("automatic mutation lease"))),
                    ParentNodeKey: $"project:{projectId:D}",
                    ObjectSubtype: "txt")
            });

        Assert.Equal($"project:{projectId:D}", asset.ParentId);
        Assert.Null(await leaseService.GetActiveLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString("D")));
    }

    [Fact]
    public async Task Auto_approved_noninteractive_tools_do_not_expose_explicit_lease_tokens()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var projectId = await CreateProjectAsync(projects);

        var tools = await CreateToolsAsync(
            scope.ServiceProvider,
            projectId,
            AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive);

        Assert.DoesNotContain(
            tools,
            tool => ExplicitLeaseToolNames.Contains(tool.Name, StringComparer.Ordinal));
        Assert.Contains(
            tools,
            tool => string.Equals(
                tool.Name,
                AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Analytics_query_tool_exposes_only_the_agent_safe_allowlist()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var analyticsService = scope.ServiceProvider.GetRequiredService<ProjectStructureAnalyticsService>();
        var projectId = await CreateProjectAsync(projects);
        var sentinel = CreateAnalyticsSentinel(projectId);
        await analyticsService.RecordAsync(sentinel.Request);
        var tools = await CreateToolsAsync(scope.ServiceProvider, projectId);
        var tool = FindTool(
            tools,
            AgentToolInvocationPolicyMetadata.ProjectStructureAnalyticsQuery);
        var function = Assert.IsAssignableFrom<AIFunction>(tool);

        var rawResult = await function.InvokeAsync(
            new AIFunctionArguments
            {
                ["request"] = new ProjectStructureAnalyticsQueryRequest(
                    ProjectId: projectId,
                    OperationName: sentinel.Request.OperationName,
                    Take: 1)
            });
        var response = rawResult switch
        {
            ProjectStructureAgentAnalyticsResponse typed => typed,
            JsonElement json => JsonSerializer.Deserialize<ProjectStructureAgentAnalyticsResponse>(
                json.GetRawText(),
                FunctionResultJsonOptions)!,
            _ => throw new InvalidOperationException(
                $"Tool '{tool.Name}' returned unexpected result type '{rawResult?.GetType().FullName ?? "<null>"}'.")
        };
        var entry = Assert.Single(response.Entries);
        var serialized = rawResult switch
        {
            JsonElement json => json.GetRawText(),
            null => "null",
            _ => JsonSerializer.Serialize(
                rawResult,
                rawResult.GetType(),
                FunctionResultJsonOptions)
        };
        using var document = JsonDocument.Parse(serialized);
        var responseProperties = document.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        var serializedEntry = Assert.Single(
            document.RootElement
                .GetProperty("entries")
                .EnumerateArray()
                .ToArray());
        var propertyNames = serializedEntry
            .EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expectedPropertyNames =
        [
            "durationMs",
            "errorCode",
            "nodeKey",
            "occurredAtUtc",
            "operationName",
            "projectId",
            "scopeKind",
            "succeeded",
            "warningCount"
        ];

        Assert.Equal(sentinel.Request.OperationName, entry.OperationName);
        Assert.Equal(projectId, entry.ProjectId);
        Assert.Equal(sentinel.Request.NodeKey, entry.NodeKey);
        Assert.Equal(ProjectStructureLeaseScopeKind.Project, entry.ScopeKind);
        Assert.False(entry.Succeeded);
        Assert.Equal(sentinel.Request.DurationMs, entry.DurationMs);
        Assert.Equal(sentinel.Request.Warnings.Count, entry.WarningCount);
        Assert.Equal(
            ProjectStructureAgentAnalyticsBoundary.OperationFailedErrorCode,
            entry.ErrorCode);
        Assert.NotEqual(default, entry.OccurredAtUtc);
        Assert.Equal(["entries"], responseProperties);
        Assert.Equal(expectedPropertyNames.Order(StringComparer.Ordinal), propertyNames);
        Assert.All(
            sentinel.ExcludedValues,
            excludedValue => Assert.DoesNotContain(
                excludedValue,
                serialized,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Analytics_service_query_retains_protected_operator_diagnostics()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var analyticsService = scope.ServiceProvider.GetRequiredService<ProjectStructureAnalyticsService>();
        var projectId = await CreateProjectAsync(projects);
        var sentinel = CreateAnalyticsSentinel(projectId);
        await analyticsService.RecordAsync(sentinel.Request);

        var response = await analyticsService.QueryAsync(
            new ProjectStructureAnalyticsQueryRequest(
                ProjectId: projectId,
                OperationName: sentinel.Request.OperationName,
                Take: 1));

        var entry = Assert.Single(response.Entries);
        Assert.Equal(sentinel.Request.ScopeKey, entry.ScopeKey);
        Assert.Equal(sentinel.Request.Agent.AgentId, entry.AgentId);
        Assert.Equal(sentinel.Request.Agent.AgentName, entry.AgentName);
        Assert.Equal(sentinel.Request.Agent.MachineName, entry.MachineName);
        Assert.Equal(sentinel.Request.Agent.RepositoryRoot, entry.RepositoryRoot);
        Assert.Equal(sentinel.Request.Agent.BranchName, entry.BranchName);
        Assert.Equal(sentinel.Request.ErrorCode, entry.ErrorCode);
        Assert.Equal(sentinel.Request.ErrorMessage, entry.ErrorMessage);
        Assert.Equal(sentinel.Request.RequestSummaryJson, entry.RequestSummaryJson);
        Assert.Equal(sentinel.Request.ResponseSummaryJson, entry.ResponseSummaryJson);
        Assert.Contains(
            sentinel.Request.Warnings[0],
            entry.WarningsJson,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Asset_create_contract_requires_an_explicit_parent_node_key()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var projectId = await CreateProjectAsync(projects);
        var tools = await CreateToolsAsync(scope.ServiceProvider, projectId);
        var assetCreate = Assert.IsAssignableFrom<AIFunction>(
            FindTool(tools, "project_structure_asset_create"));
        var requestSchema = assetCreate.JsonSchema
            .GetProperty("properties")
            .GetProperty("request");
        var requiredProperties = requestSchema
            .GetProperty("required")
            .EnumerateArray()
            .Select(property => property.GetString())
            .ToArray();

        Assert.Contains("parentNodeKey", requiredProperties);
    }

    [Fact]
    public async Task Delete_tools_require_a_storage_disposition_and_round_trip_both_file_outcomes()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var workspacePaths = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>();
        var projectId = await CreateProjectAsync(projects);
        var tools = await CreateToolsAsync(scope.ServiceProvider, projectId);
        var singleDelete = Assert.IsAssignableFrom<AIFunction>(
            FindTool(tools, AgentToolInvocationPolicyMetadata.ProjectStructureNodeDelete));
        var batchDelete = Assert.IsAssignableFrom<AIFunction>(
            FindTool(tools, AgentToolInvocationPolicyMetadata.ProjectStructureNodesDelete));

        AssertDeleteDispositionRequired(singleDelete);
        AssertDeleteDispositionRequired(batchDelete);
        Assert.Contains("RetainManagedFiles", singleDelete.Description, StringComparison.Ordinal);
        Assert.Contains("DeleteOwnedManagedFiles", batchDelete.Description, StringComparison.Ordinal);

        var retainedOne = await CreateTextAssetAsync(tools, projectId, "Retain one");
        var retainedTwo = await CreateTextAssetAsync(tools, projectId, "Retain two");
        var retainedPaths = new[] { retainedOne, retainedTwo }
            .Select(node => Path.Combine(
                workspacePaths.ResolveWorkspaceRoot(),
                node.MediaRelativePath!.Replace('/', Path.DirectorySeparatorChar)))
            .ToArray();
        Assert.All(retainedPaths, path => Assert.True(File.Exists(path)));

        var unspecifiedException = await Assert.ThrowsAsync<ProjectStructureAgentException>(
            () => InvokeAsync<OperationCount>(
                singleDelete,
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["nodeId"] = retainedOne.Id,
                    ["request"] = new ProjectStructureNodeDeleteInput(
                        ProjectStructureManagedStorageDisposition.Unspecified)
                }));
        Assert.Equal(
            "ProjectStructureManagedStorageDispositionRequired",
            unspecifiedException.ErrorCode);
        Assert.True(unspecifiedException.IsSafeToExpose);
        Assert.True(unspecifiedException.CanRetryWithCorrectedInput);
        Assert.All(retainedPaths, path => Assert.True(File.Exists(path)));

        var retainedResult = await InvokeAsync<OperationCount>(
            batchDelete,
            new AIFunctionArguments
            {
                ["projectId"] = projectId,
                ["request"] = new ProjectStructureNodeDeleteBatchInput(
                    [retainedOne.Id, retainedTwo.Id],
                    ProjectStructureManagedStorageDisposition.RetainManagedFiles)
            });

        Assert.Equal(2, retainedResult.Count);
        Assert.All(retainedPaths, path => Assert.True(File.Exists(path)));

        var deletedAsset = await CreateTextAssetAsync(tools, projectId, "Delete owned");
        var deletedPath = Path.Combine(
            workspacePaths.ResolveWorkspaceRoot(),
            deletedAsset.MediaRelativePath!.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(deletedPath));

        var deletedResult = await InvokeAsync<OperationCount>(
            singleDelete,
            new AIFunctionArguments
            {
                ["projectId"] = projectId,
                ["nodeId"] = deletedAsset.Id,
                ["request"] = new ProjectStructureNodeDeleteInput(
                    ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles)
            });

        Assert.Equal(1, deletedResult.Count);
        Assert.False(File.Exists(deletedPath));
        var surface = await workbench.GetStructureAsync(projectId);
        Assert.DoesNotContain(surface.Nodes, node => node.Id == retainedOne.Id);
        Assert.DoesNotContain(surface.Nodes, node => node.Id == retainedTwo.Id);
        Assert.DoesNotContain(surface.Nodes, node => node.Id == deletedAsset.Id);
    }

    [Fact]
    public async Task Asset_create_rejects_an_omitted_parent_without_mutating_the_project()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projects);
        var tools = await CreateToolsAsync(scope.ServiceProvider, projectId);
        var before = await workbench.GetStructureAsync(projectId);

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(
            () => InvokeAsync<ProjectStructureNodeSummary>(
                FindTool(tools, "project_structure_asset_create"),
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["request"] = new ProjectStructureAgentAssetCreateInput(
                        ProjectObjectType.File,
                        "Parentless planning note",
                        "Invalid placement probe",
                        "This asset must not silently fall back to the project root.",
                        new ProjectObjectMediaPayload(
                            "parentless.md",
                            "text/markdown",
                            Convert.ToBase64String(Encoding.UTF8.GetBytes("# Parentless"))),
                        ObjectSubtype: "md")
                }));

        Assert.Equal("AssetParentRequired", exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
        var after = await workbench.GetStructureAsync(projectId);
        Assert.Equal(before.Nodes.Count, after.Nodes.Count);
    }

    [Fact]
    public async Task Generic_node_create_contract_routes_mermaid_to_managed_assets()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var projectId = await CreateProjectAsync(projects);
        var tools = await CreateToolsAsync(scope.ServiceProvider, projectId);
        var nodeCreate = FindTool(tools, "project_structure_node_create");

        Assert.Contains(
            "project_structure_asset_create",
            nodeCreate.Description,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generic_node_create_contract_does_not_instruct_notes_only_mermaid()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var projectId = await CreateProjectAsync(projects);
        var tools = await CreateToolsAsync(scope.ServiceProvider, projectId);
        var nodeCreate = FindTool(tools, "project_structure_node_create");

        Assert.DoesNotContain(
            "Mermaid source in notes",
            nodeCreate.Description,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Generic_node_create_rejects_mermaid_notes_without_mutating_the_project()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projects);
        var tools = await CreateToolsAsync(scope.ServiceProvider, projectId);
        var before = await workbench.GetStructureAsync(projectId);

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(
            () => InvokeAsync<ProjectStructureNodeSummary>(
                FindTool(tools, "project_structure_node_create"),
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["request"] = new ProjectStructureNodeCreateInput(
                        ProjectObjectType.File,
                        "Planning flow",
                        "Invalid notes-only Mermaid probe",
                        "flowchart LR\n    Idea --> Plan",
                        $"project:{projectId:D}",
                        ObjectSubtype: "mermaid")
                }));

        Assert.Equal("ManagedAssetCreationRequired", exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
        var after = await workbench.GetStructureAsync(projectId);
        Assert.Equal(before.Nodes.Count, after.Nodes.Count);
    }

    [Fact]
    public async Task Asset_create_round_trips_mermaid_source_as_managed_content()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var projectId = await CreateProjectAsync(projects);
        var tools = await CreateToolsAsync(scope.ServiceProvider, projectId);
        const string mermaid = "flowchart LR\n    Idea --> Plan";

        var assetNode = await InvokeAsync<ProjectStructureNodeSummary>(
            FindTool(tools, "project_structure_asset_create"),
            new AIFunctionArguments
            {
                ["projectId"] = projectId,
                ["request"] = new ProjectStructureAgentAssetCreateInput(
                    ProjectObjectType.File,
                    "Planning flow",
                    "Managed Mermaid asset",
                    "Diagram source is stored in managed asset content.",
                    new ProjectObjectMediaPayload(
                        "planning-flow.mmd",
                        ProjectStructureFileInteractionPolicy.MermaidMediaType,
                        Convert.ToBase64String(Encoding.UTF8.GetBytes(mermaid))),
                    ParentNodeKey: $"project:{projectId:D}",
                    ObjectSubtype: "mermaid")
            });
        var content = await InvokeAsync<ProjectStructureAssetContentDescriptor>(
            FindTool(tools, "project_structure_asset_content_get"),
            new AIFunctionArguments
            {
                ["projectId"] = projectId,
                ["nodeId"] = assetNode.Id
            });

        Assert.Equal(ProjectObjectType.File, assetNode.ObjectType);
        Assert.Equal($"project:{projectId:D}", assetNode.ParentId);
        Assert.Equal("mermaid", content.Asset.ObjectSubtype);
        Assert.Equal("planning-flow.mmd", content.Asset.MediaOriginalFileName);
        Assert.Equal(
            ProjectStructureFileInteractionPolicy.MermaidMediaType,
            content.Asset.MediaContentType);
        Assert.Equal(
            MermaidDiagramKind.Flowchart,
            ProjectObjectMetadataSerializer.Parse(content.Asset.MetadataJson).File?.MermaidDiagramKind);
        Assert.Equal(
            mermaid,
            Encoding.UTF8.GetString(Convert.FromBase64String(content.Base64Data)));
    }

    [Fact]
    public async Task Runtime_tools_round_trip_selected_subtree_non_task_node_and_managed_markdown_asset()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();
        var workspacePaths = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolutionService>();
        var provider = scope.ServiceProvider
            .GetServices<IAgentRuntimeToolProvider>()
            .OfType<ProjectStructureAgentRuntimeToolProvider>()
            .Single();

        var projectId = await CreateProjectAsync(projects);
        var selectedNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Main Architecture",
                "Selected architecture container",
                string.Empty,
                $"project:{projectId:D}",
                ObjectSubtype: "architecture"));
        var architectureDetail = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Browser application",
                "Blazor WebAssembly PWA",
                "The game loop runs in the browser and persists state in IndexedDB.",
                selectedNode.Id,
                ObjectSubtype: "implementation"));
        var agent = CreateAgent(projectId);
        var tools = await provider.CreateToolsAsync(
            CreateContext(
                agent,
                projectId,
                AgentRuntimeToolProviderPurpose.GovernedProcessAutomation),
            CancellationToken.None);

        Assert.Contains(tools, tool => tool.Name == "project_structure_read");
        Assert.Contains(tools, tool => tool.Name == "project_structure_node_create");
        Assert.Contains(tools, tool => tool.Name == "project_structure_node_update");
        Assert.Contains(tools, tool => tool.Name == "project_structure_asset_create");
        Assert.Contains(tools, tool => tool.Name == "project_structure_asset_get");
        Assert.Contains(tools, tool => tool.Name == "project_structure_asset_content_get");
        Assert.Contains(tools, tool => tool.Name == "project_structure_asset_text_get");
        Assert.All(
            ExplicitLeaseToolNames,
            toolName => Assert.Contains(
                tools,
                tool => string.Equals(tool.Name, toolName, StringComparison.Ordinal)));
        Assert.DoesNotContain(tools, tool => tool.Name == "project_task_create");
        Assert.DoesNotContain(tools, tool => tool.Name == "project_task_update");
        Assert.DoesNotContain(tools, tool => tool.Name.StartsWith("workspace_", StringComparison.Ordinal));

        var assetCreate = Assert.IsAssignableFrom<AIFunction>(
            FindTool(tools, "project_structure_asset_create"));
        var assetCreateRequestSchema = assetCreate.JsonSchema
            .GetProperty("properties")
            .GetProperty("request")
            .GetProperty("properties");
        Assert.False(assetCreateRequestSchema.TryGetProperty("metadataJson", out _));
        var assetRevision = Assert.IsAssignableFrom<AIFunction>(
            FindTool(tools, "project_structure_asset_create_revision"));
        var assetRevisionRequestSchema = assetRevision.JsonSchema
            .GetProperty("properties")
            .GetProperty("request")
            .GetProperty("properties");
        Assert.False(assetRevisionRequestSchema.TryGetProperty("metadataJson", out _));

        var selectedSubtree = await InvokeAsync<ProjectStructureReadToolData>(
            FindTool(tools, "project_structure_read"),
            new AIFunctionArguments
            {
                ["projectId"] = projectId,
                ["request"] = new ProjectStructureReadRequest(
                    SubtreeRootIds: [selectedNode.Id],
                    IncludeLinks: true,
                    IncludeMetadata: true,
                    IncludeNotes: true,
                    IncludeAssets: true,
                    Source: ProjectStructureReadSource.CanonicalCurrent)
            });

        Assert.Collection(
            selectedSubtree.Nodes.OrderBy(node => node.Id == selectedNode.Id ? 0 : 1),
            node =>
            {
                Assert.Equal(selectedNode.Id, node.Id);
                Assert.Equal("Main Architecture", node.Title);
            },
            node =>
            {
                Assert.Equal(architectureDetail.Id, node.Id);
                Assert.Equal(selectedNode.Id, node.ParentId);
                Assert.Contains("IndexedDB", node.Notes, StringComparison.Ordinal);
            });

        ProjectStructureLeaseSnapshot? lease = null;
        ProjectStructureLeaseSnapshot? releasedLease = null;
        string? sourceFilePath = null;
        string? sourceDirectoryPath = null;
        try
        {
            lease = await InvokeAsync<ProjectStructureLeaseSnapshot>(
                FindTool(
                    tools,
                    AgentToolInvocationPolicyMetadata.ProjectStructureProjectLeaseAcquire),
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["reason"] = "Validate selected project-structure agent operations",
                    ["durationMinutes"] = 5
                });
            var projectLeaseScope = new ProjectStructureScopeInput(
                ProjectStructureLeaseScopeKind.Project,
                ProjectId: projectId);
            var activeLease = await InvokeAsync<ProjectStructureLeaseSnapshot>(
                FindTool(
                    tools,
                    AgentToolInvocationPolicyMetadata.ProjectStructureLeaseGet),
                new AIFunctionArguments
                {
                    ["scope"] = projectLeaseScope
                });

            Assert.Equal(lease.LeaseToken, activeLease.LeaseToken);
            Assert.True(activeLease.IsActive);

            var renewedLease = await InvokeAsync<ProjectStructureLeaseSnapshot>(
                FindTool(
                    tools,
                    AgentToolInvocationPolicyMetadata.ProjectStructureLeaseRenew),
                new AIFunctionArguments
                {
                    ["scope"] = projectLeaseScope,
                    ["leaseToken"] = lease.LeaseToken,
                    ["durationMinutes"] = 5
                });

            Assert.Equal(lease.LeaseToken, renewedLease.LeaseToken);
            Assert.True(renewedLease.IsActive);
            Assert.True(renewedLease.ExpiresAtUtc >= lease.ExpiresAtUtc);
            lease = renewedLease;

            var invalidMetadataException = await Assert.ThrowsAsync<ProjectStructureAgentException>(
                async () => await Assert.IsAssignableFrom<AIFunction>(
                        FindTool(tools, "project_structure_node_create"))
                    .InvokeAsync(
                        new AIFunctionArguments
                        {
                            ["projectId"] = projectId,
                            ["request"] = new ProjectStructureNodeCreateInput(
                                ProjectObjectType.ProjectBlock,
                                "Invalid metadata probe",
                                "Must not be persisted",
                                "Exercises sanitized agent feedback.",
                                selectedNode.Id,
                                ObjectSubtype: "delivery",
                                MetadataJson: """{"workflow":"delivery"}""",
                                LeaseToken: lease.LeaseToken)
                        }));

            Assert.Equal("InvalidProjectObjectMetadata", invalidMetadataException.ErrorCode);
            Assert.Contains("$.workflow", invalidMetadataException.SafeMessage, StringComparison.Ordinal);
            Assert.True(invalidMetadataException.IsSafeToExpose);
            Assert.True(invalidMetadataException.CanRetryWithCorrectedInput);

            var createdNode = await InvokeAsync<ProjectStructureNodeSummary>(
                FindTool(tools, "project_structure_node_create"),
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["request"] = new ProjectStructureNodeCreateInput(
                        ProjectObjectType.ProjectBlock,
                        "Architecture summary draft",
                        "Agent-created architecture summary",
                        "Drafted from the selected architecture subtree.",
                        selectedNode.Id,
                        ObjectSubtype: "architecture",
                        LeaseToken: lease.LeaseToken)
                });

            Assert.Equal(selectedNode.Id, createdNode.ParentId);
            Assert.Equal(ProjectObjectType.ProjectBlock, createdNode.ObjectType);

            var updatedNode = await InvokeAsync<ProjectStructureNodeSummary>(
                FindTool(tools, "project_structure_node_update"),
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["nodeId"] = createdNode.Id,
                    ["request"] = new ProjectStructureNodeEditInput(
                        "Architecture summary",
                        "Agent-maintained architecture summary",
                        "Validated against the selected architecture subtree.",
                        ProjectObjectType.ProjectBlock,
                        "architecture",
                        LeaseToken: lease.LeaseToken)
                });

            Assert.Equal(createdNode.Id, updatedNode.Id);
            Assert.Equal("Architecture summary", updatedNode.Title);

            const string markdown = """
                # Main Architecture

                - Blazor WebAssembly PWA
                - Browser game loop
                - IndexedDB persistence
                """;
            var sourceRelativePath = $"artifacts/process-runs/{Guid.NewGuid():N}/main-architecture.md";
            var sourceResolution = workspacePaths.ResolveFilePath(sourceRelativePath, allowMissing: true);
            sourceFilePath = sourceResolution.FullPath;
            sourceDirectoryPath = Path.GetDirectoryName(sourceFilePath)!;
            Directory.CreateDirectory(sourceDirectoryPath);
            await File.WriteAllTextAsync(
                sourceResolution.FullPath,
                markdown,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            var assetNode = await InvokeAsync<ProjectStructureNodeSummary>(
                FindTool(tools, "project_structure_asset_create"),
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["request"] = new ProjectStructureAgentAssetCreateInput(
                        ProjectObjectType.File,
                        "Main architecture summary",
                        "Generated from the selected architecture subtree",
                        "Architecture summary stored as a project asset.",
                        Media: null,
                        ParentNodeKey: selectedNode.Id,
                        ObjectSubtype: "md",
                        LeaseToken: lease.LeaseToken,
                        SourceWorkspacePath: sourceRelativePath,
                        SourceFileName: "main-architecture.md",
                        SourceContentType: "text/markdown")
                });

            Assert.Equal(selectedNode.Id, assetNode.ParentId);
            Assert.Equal(ProjectObjectType.File, assetNode.ObjectType);
            Assert.DoesNotContain("external-target/", sourceRelativePath, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("artifacts/scopes/organization/", sourceResolution.RelativePath, StringComparison.Ordinal);

            var asset = await InvokeAsync<ProjectStructureAssetDescriptor>(
                FindTool(tools, "project_structure_asset_get"),
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["nodeId"] = assetNode.Id
                });
            var assetContent = await InvokeAsync<ProjectStructureAssetContentDescriptor>(
                FindTool(tools, "project_structure_asset_content_get"),
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["nodeId"] = assetNode.Id
                });

            Assert.Equal("main-architecture.md", asset.MediaOriginalFileName);
            Assert.Equal("text/markdown", asset.MediaContentType);
            Assert.False(assetContent.Base64DataOmitted);
            Assert.Equal(
                markdown,
                Encoding.UTF8.GetString(Convert.FromBase64String(assetContent.Base64Data)));

            const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><text>Calculator layout</text></svg>";
            var svgNode = await InvokeAsync<ProjectStructureNodeSummary>(
                FindTool(tools, "project_structure_asset_create"),
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["request"] = new ProjectStructureAgentAssetCreateInput(
                        ProjectObjectType.ImageAsset,
                        "Calculator layout proposal",
                        "Generated SVG",
                        "Safe textual image asset.",
                        new ProjectObjectMediaPayload(
                            "calculator-layout-proposal.svg",
                            "image/svg+xml",
                            Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))),
                        ParentNodeKey: selectedNode.Id,
                        ObjectSubtype: "svg",
                        LeaseToken: lease.LeaseToken)
                });
            var svgText = await InvokeAsync<ProjectStructureAssetTextDescriptor>(
                FindTool(tools, "project_structure_asset_text_get"),
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["nodeId"] = svgNode.Id
                });

            Assert.Equal(svg, svgText.TextContent);
            Assert.Equal("image/svg+xml", svgText.Asset.MediaContentType);
            Assert.False(svgText.IsTruncated);

            var readback = await InvokeAsync<ProjectStructureReadToolData>(
                FindTool(tools, "project_structure_read"),
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["request"] = new ProjectStructureReadRequest(
                        SubtreeRootIds: [selectedNode.Id],
                        IncludeLinks: true,
                        IncludeMetadata: true,
                        IncludeNotes: true,
                        IncludeAssets: true,
                        Source: ProjectStructureReadSource.CanonicalCurrent)
                });

            var readbackNode = Assert.Single(readback.Nodes, node => node.Id == updatedNode.Id);
            Assert.Equal("Architecture summary", readbackNode.Title);
            Assert.Equal("Validated against the selected architecture subtree.", readbackNode.Notes);
            var readbackAsset = Assert.Single(readback.Nodes, node => node.Id == assetNode.Id);
            Assert.Equal(selectedNode.Id, readbackAsset.ParentId);
            Assert.Equal("main-architecture.md", readbackAsset.MediaOriginalFileName);
        }
        finally
        {
            if (lease is not null)
            {
                releasedLease = await InvokeAsync<ProjectStructureLeaseSnapshot>(
                    FindTool(
                        tools,
                        AgentToolInvocationPolicyMetadata.ProjectStructureLeaseRelease),
                    new AIFunctionArguments
                    {
                        ["scope"] = new ProjectStructureScopeInput(
                            ProjectStructureLeaseScopeKind.Project,
                            ProjectId: projectId),
                        ["leaseToken"] = lease.LeaseToken
                    });
            }

            if (sourceFilePath is not null && File.Exists(sourceFilePath))
            {
                File.Delete(sourceFilePath);
            }

            if (sourceDirectoryPath is not null &&
                Directory.Exists(sourceDirectoryPath) &&
                !Directory.EnumerateFileSystemEntries(sourceDirectoryPath).Any())
            {
                Directory.Delete(sourceDirectoryPath, recursive: false);
            }
        }

        Assert.NotNull(releasedLease);
        Assert.False(releasedLease.IsActive);
        Assert.Null(await leaseService.GetActiveLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString("D")));
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projects)
    {
        var result = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Agent project-structure round trip",
            Description = "Isolated project for agent-facing project-structure integration coverage.",
            Objective = "Prove selected subtree, non-task node, and managed asset operations.",
            CurrentPhase = "Validation"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static void AssertDeleteDispositionRequired(AIFunction function)
    {
        var requestSchema = function.JsonSchema
            .GetProperty("properties")
            .GetProperty("request");
        var requiredProperties = requestSchema
            .GetProperty("required")
            .EnumerateArray()
            .Select(property => property.GetString())
            .ToArray();

        Assert.Contains("managedStorageDisposition", requiredProperties);
    }

    private static Task<ProjectStructureNodeSummary> CreateTextAssetAsync(
        IReadOnlyList<AITool> tools,
        Guid projectId,
        string title)
        => InvokeAsync<ProjectStructureNodeSummary>(
            FindTool(tools, AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate),
            new AIFunctionArguments
            {
                ["projectId"] = projectId,
                ["request"] = new ProjectStructureAgentAssetCreateInput(
                    ProjectObjectType.File,
                    title,
                    "Deletion disposition proof",
                    "Managed content for runtime-tool deletion coverage.",
                    new ProjectObjectMediaPayload(
                        $"{title.Replace(' ', '-').ToLowerInvariant()}.txt",
                        "text/plain",
                        Convert.ToBase64String(Encoding.UTF8.GetBytes(title))),
                    ParentNodeKey: $"project:{projectId:D}",
                    ObjectSubtype: "txt")
            });

    private static AnalyticsSentinel CreateAnalyticsSentinel(Guid projectId)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var providerPayload = $"excluded-provider-{suffix}";
        var sessionPayload = $"excluded-session-{suffix}";
        var toolPayload = $"excluded-tool-{suffix}";
        var warning = $"excluded-warning-{suffix}";
        var request = new ProjectStructureAnalyticsWriteRequest(
            $"sentinel.analytics.{suffix}",
            projectId,
            $"safe-node-{suffix}",
            ProjectStructureLeaseScopeKind.Project,
            $"excluded-scope-{suffix}",
            new ProjectStructureAgentContext(
                $"excluded-agent-id-{suffix}",
                $"excluded-agent-name-{suffix}",
                $"excluded-machine-{suffix}",
                $"C:/excluded-repository-{suffix}",
                $"excluded-branch-{suffix}",
                sessionPayload),
            Succeeded: false,
            DurationMs: 42,
            Warnings: [warning],
            ErrorCode: $"excluded-unreviewed-error-code-{suffix}",
            ErrorMessage: $"excluded-error-message-{suffix}",
            RequestSummaryJson: JsonSerializer.Serialize(new
            {
                ProviderPayload = providerPayload,
                SessionPayload = sessionPayload,
                ToolPayload = toolPayload
            }),
            ResponseSummaryJson: JsonSerializer.Serialize(new
            {
                ProviderPayload = providerPayload,
                SessionPayload = sessionPayload,
                ToolPayload = toolPayload
            }));

        return new AnalyticsSentinel(
            request,
            [
                request.ScopeKey!,
                request.Agent.AgentId,
                request.Agent.AgentName,
                request.Agent.MachineName,
                request.Agent.RepositoryRoot,
                request.Agent.BranchName,
                request.Agent.SessionId,
                request.ErrorCode!,
                request.ErrorMessage!,
                providerPayload,
                sessionPayload,
                toolPayload,
                warning
            ]);
    }

    private static AgentDefinition CreateAgent(Guid projectId)
    {
        var now = DateTimeOffset.UtcNow;
        var configurationJson = AgentProjectStructureAccessMetadata.Write(
            "{}",
            new AgentProjectStructureAccessSettings
            {
                CanRead = true,
                CanWrite = false,
                CanWriteNonTaskStructure = true,
                CanWriteTasks = false,
                AllowAllProjects = false,
                AllowedProjectIds = [projectId]
            });

        return new AgentDefinition(
            Guid.NewGuid(),
            "Project Structure Integration Agent",
            "Portfolio architect",
            "Exercises the project-structure runtime tool boundary.",
            "Use selected project-structure context and store generated files as project assets.",
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

    private static AgentRuntimeToolProviderContext CreateContext(
        AgentDefinition agent,
        Guid projectId,
        AgentRuntimeToolProviderPurpose purpose = AgentRuntimeToolProviderPurpose.InteractiveChat)
    {
        var provider = new ProviderProfile(
            agent.ProviderProfileId!.Value,
            "Integration provider",
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

        return new AgentRuntimeToolProviderContext(
            agent,
            provider,
            [],
            SuppressApprovalRequirements: false,
            purpose,
            RuntimeSessionKey: $"project-structure-integration:{projectId:D}",
            intent,
            Tags: new Dictionary<string, string>());
    }

    private static async Task<IReadOnlyList<AITool>> CreateToolsAsync(
        IServiceProvider services,
        Guid projectId,
        AgentRuntimeToolProviderPurpose purpose = AgentRuntimeToolProviderPurpose.InteractiveChat)
    {
        var provider = services
            .GetServices<IAgentRuntimeToolProvider>()
            .OfType<ProjectStructureAgentRuntimeToolProvider>()
            .Single();

        return await provider.CreateToolsAsync(
            CreateContext(CreateAgent(projectId), projectId, purpose),
            CancellationToken.None);
    }

    private static JsonSerializerOptions CreateFunctionResultJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static AITool FindTool(IReadOnlyList<AITool> tools, string name)
    {
        return Assert.Single(tools, tool => string.Equals(tool.Name, name, StringComparison.Ordinal));
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

    private sealed record AnalyticsSentinel(
        ProjectStructureAnalyticsWriteRequest Request,
        IReadOnlyList<string> ExcludedValues);
}
