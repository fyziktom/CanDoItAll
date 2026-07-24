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
        Assert.DoesNotContain(tools, tool => tool.Name == "project_task_create");
        Assert.DoesNotContain(tools, tool => tool.Name == "project_task_update");
        Assert.DoesNotContain(tools, tool => tool.Name.StartsWith("workspace_", StringComparison.Ordinal));

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
                    IncludeAssets: true)
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
                    ["request"] = new ProjectStructureAssetCreateInput(
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
                        IncludeAssets: true)
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
