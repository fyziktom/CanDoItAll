using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureAgentRuntimeToolRoundTripIntegrationTests
{
    private static readonly JsonSerializerOptions FunctionResultJsonOptions = CreateFunctionResultJsonOptions();

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
            CreateContext(agent, projectId),
            CancellationToken.None);

        Assert.Contains(tools, tool => tool.Name == "project_structure_read");
        Assert.Contains(tools, tool => tool.Name == "project_structure_node_create");
        Assert.Contains(tools, tool => tool.Name == "project_structure_node_update");
        Assert.Contains(tools, tool => tool.Name == "project_structure_asset_create");
        Assert.Contains(tools, tool => tool.Name == "project_structure_asset_get");
        Assert.Contains(tools, tool => tool.Name == "project_structure_asset_content_get");
        Assert.Contains(tools, tool => tool.Name == "project_structure_asset_text_get");
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
                FindTool(tools, "project_structure_project_lease_acquire"),
                new AIFunctionArguments
                {
                    ["projectId"] = projectId,
                    ["reason"] = "Validate selected project-structure agent operations",
                    ["durationMinutes"] = 5
                });

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
                    FindTool(tools, "project_structure_lease_release"),
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

    private static AgentRuntimeToolProviderContext CreateContext(AgentDefinition agent, Guid projectId)
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
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            RuntimeSessionKey: $"project-structure-integration:{projectId:D}",
            intent,
            Tags: new Dictionary<string, string>());
    }

    private static async Task<IReadOnlyList<AITool>> CreateToolsAsync(
        IServiceProvider services,
        Guid projectId)
    {
        var provider = services
            .GetServices<IAgentRuntimeToolProvider>()
            .OfType<ProjectStructureAgentRuntimeToolProvider>()
            .Single();

        return await provider.CreateToolsAsync(
            CreateContext(CreateAgent(projectId), projectId),
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
}
