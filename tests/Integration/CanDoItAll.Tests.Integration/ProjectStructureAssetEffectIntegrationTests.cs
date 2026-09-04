using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectStructureAssetEffectIntegrationTests
{
    private static readonly JsonSerializerOptions FunctionResultJsonOptions = CreateFunctionResultJsonOptions();

    [Fact]
    public async Task Valid_managed_asset_commit_is_visible_in_canonical_readback()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectId = await CreateProjectAsync(scope.ServiceProvider.GetRequiredService<ProjectsService>());
        var tools = await CreateToolsAsync(scope.ServiceProvider, projectId);

        var created = await CreateAssetAsync(tools, projectId, "Committed architecture overview");
        var surface = await scope.ServiceProvider
            .GetRequiredService<ProjectWorkbenchService>()
            .GetStructureAsync(projectId);
        var canonical = Assert.Single(surface.Nodes, node => node.Id == created.Id);

        Assert.Equal($"project:{projectId:D}", canonical.ParentId);
        Assert.Equal(ProjectObjectType.File, canonical.ObjectType);
        var evidence = Assert.IsAssignableFrom<IAgentToolInvocationResultEvidence>(created);
        Assert.Equal(AgentToolInvocationOutcome.Succeeded, evidence.Outcome);
        Assert.Equal(AgentToolEffectState.Committed, evidence.EffectState);
    }

    [Fact]
    public async Task Invalid_parent_fails_without_creating_an_asset()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectId = await CreateProjectAsync(scope.ServiceProvider.GetRequiredService<ProjectsService>());
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var tools = await CreateToolsAsync(scope.ServiceProvider, projectId);
        var before = await workbench.GetStructureAsync(projectId);

        await Assert.ThrowsAsync<ProjectStructureAgentException>(() => InvokeAsync<ProjectStructureNodeSummary>(
            FindAssetCreateTool(tools),
            CreateArguments(projectId, "Invalid parent asset", "missing-parent")));

        var after = await workbench.GetStructureAsync(projectId);
        Assert.Equal(before.Nodes.Select(node => node.Id).Order(), after.Nodes.Select(node => node.Id).Order());
    }

    [Fact]
    public async Task Foreign_project_and_escaping_workspace_path_are_rejected_without_effect()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var allowedProjectId = await CreateProjectAsync(projects);
        var foreignProjectId = await CreateProjectAsync(projects);
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var tools = await CreateToolsAsync(scope.ServiceProvider, allowedProjectId);
        var beforeAllowed = await workbench.GetStructureAsync(allowedProjectId);
        var beforeForeign = await workbench.GetStructureAsync(foreignProjectId);

        var foreign = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            InvokeAsync<ProjectStructureNodeSummary>(
                FindAssetCreateTool(tools),
                CreateArguments(
                    foreignProjectId,
                    "Foreign project asset",
                    $"project:{foreignProjectId:D}")));
        var escaping = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            InvokeAsync<ProjectStructureNodeSummary>(
                FindAssetCreateTool(tools),
                new AIFunctionArguments
                {
                    ["projectId"] = allowedProjectId,
                    ["request"] = new ProjectStructureAgentAssetCreateInput(
                        ProjectObjectType.File,
                        "Escaping workspace asset",
                        "Rejected path",
                        "This path must remain outside managed storage.",
                        Media: null,
                        ParentNodeKey: $"project:{allowedProjectId:D}",
                        ObjectSubtype: "txt",
                        SourceWorkspacePath: "../outside.txt",
                        SourceFileName: "outside.txt",
                        SourceContentType: "text/plain")
                }));

        Assert.Equal(403, foreign.StatusCode);
        Assert.True(escaping.StatusCode is 400 or 403 or 404);
        var afterAllowed = await workbench.GetStructureAsync(allowedProjectId);
        var afterForeign = await workbench.GetStructureAsync(foreignProjectId);
        Assert.Equal(beforeAllowed.Nodes.Select(node => node.Id).Order(), afterAllowed.Nodes.Select(node => node.Id).Order());
        Assert.Equal(beforeForeign.Nodes.Select(node => node.Id).Order(), afterForeign.Nodes.Select(node => node.Id).Order());
    }

    [Fact]
    public async Task Analytics_failure_after_commit_does_not_mask_the_created_asset()
    {
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<IProjectStructureAnalyticsService>();
                services.AddSingleton<IProjectStructureAnalyticsService>(new ThrowingAnalyticsSink());
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projectId = await CreateProjectAsync(scope.ServiceProvider.GetRequiredService<ProjectsService>());
        var tools = await CreateToolsAsync(scope.ServiceProvider, projectId);

        var created = await CreateAssetAsync(tools, projectId, "Analytics failure asset");
        var surface = await scope.ServiceProvider
            .GetRequiredService<ProjectWorkbenchService>()
            .GetStructureAsync(projectId);

        Assert.Contains(surface.Nodes, node => node.Id == created.Id);
        Assert.Equal(
            AgentToolEffectState.Committed,
            Assert.IsAssignableFrom<IAgentToolInvocationResultEvidence>(created).EffectState);
    }

    [Fact]
    public async Task Cancellation_after_commit_does_not_erase_the_committed_result()
    {
        var analytics = new BlockingAnalyticsSink();
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<IProjectStructureAnalyticsService>();
                services.AddSingleton<IProjectStructureAnalyticsService>(analytics);
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projectId = await CreateProjectAsync(scope.ServiceProvider.GetRequiredService<ProjectsService>());
        var tools = await CreateToolsAsync(scope.ServiceProvider, projectId);
        using var cancellation = new CancellationTokenSource();
        using var effectScope = AgentToolInvocationEffectScope.Begin();

        var invocation = InvokeAsync<ProjectStructureNodeSummary>(
            FindAssetCreateTool(tools),
            CreateArguments(projectId, "Late cancellation asset", $"project:{projectId:D}"),
            cancellation.Token);
        await analytics.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        analytics.Release.TrySetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invocation);
        var surface = await scope.ServiceProvider
            .GetRequiredService<ProjectWorkbenchService>()
            .GetStructureAsync(projectId);

        Assert.False(analytics.ObservedCancellationToken.CanBeCanceled);
        Assert.Equal("project-structure", effectScope.CommittedEffect?.SourceKind);
        Assert.Equal(projectId.ToString("D"), effectScope.CommittedEffect?.SourceId);
        Assert.Contains(surface.Nodes, node => node.Title == "Late cancellation asset");
    }

    [Fact]
    public void Unverified_mutation_before_readback_is_unknown_and_not_retryable()
    {
        var assessment = MafRuntimeToolInvocationResultClassifier.Assess(
            AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate,
            ToolInvocationClassification.Mutation,
            result: null);

        Assert.Equal(AgentToolInvocationOutcome.Unknown, assessment.Outcome);
        Assert.Equal(AgentToolEffectState.Unknown, assessment.EffectState);
        Assert.False(assessment.CanRetryWithCorrectedInput);
        Assert.False(assessment.Succeeded);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projects)
    {
        var result = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = $"Asset effect {Guid.NewGuid():N}",
            Description = "Isolated project for managed asset effect tests.",
            Objective = "Prove durable mutation outcomes.",
            CurrentPhase = "Validation"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static Task<ProjectStructureNodeSummary> CreateAssetAsync(
        IReadOnlyList<AITool> tools,
        Guid projectId,
        string title)
    {
        return InvokeAsync<ProjectStructureNodeSummary>(
            FindAssetCreateTool(tools),
            CreateArguments(projectId, title, $"project:{projectId:D}"));
    }

    private static AIFunctionArguments CreateArguments(Guid projectId, string title, string parentNodeKey)
    {
        return new AIFunctionArguments
        {
            ["projectId"] = projectId,
            ["request"] = new ProjectStructureAgentAssetCreateInput(
                ProjectObjectType.File,
                title,
                "Managed text asset",
                "Created through the agent runtime tool boundary.",
                new ProjectObjectMediaPayload(
                    $"{title.Replace(' ', '-').ToLowerInvariant()}.txt",
                    "text/plain",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(title))),
                ParentNodeKey: parentNodeKey,
                ObjectSubtype: "txt")
        };
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
            "Asset effect integration agent",
            "Portfolio architect",
            "Exercises the managed project asset boundary.",
            "Create managed assets only in the active project.",
            AgentLifecycleStatus.Active,
            Guid.NewGuid(),
            "test-model",
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
            "https://provider.example.test",
            "TEST_API_KEY",
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
            RuntimeSessionKey: $"asset-effect:{projectId:D}",
            intent,
            Tags: new Dictionary<string, string>());
    }

    private static AITool FindAssetCreateTool(IReadOnlyList<AITool> tools)
    {
        return Assert.Single(tools, tool => string.Equals(
            tool.Name,
            AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate,
            StringComparison.Ordinal));
    }

    private static async Task<T> InvokeAsync<T>(
        AITool tool,
        AIFunctionArguments arguments,
        CancellationToken cancellationToken = default)
    {
        var rawResult = await Assert.IsAssignableFrom<AIFunction>(tool)
            .InvokeAsync(arguments, cancellationToken);
        return rawResult switch
        {
            T result => result,
            JsonElement json => JsonSerializer.Deserialize<T>(json.GetRawText(), FunctionResultJsonOptions)
                ?? throw new InvalidOperationException($"Tool '{tool.Name}' returned null JSON."),
            _ => throw new InvalidOperationException(
                $"Tool '{tool.Name}' returned unexpected result type '{rawResult?.GetType().FullName ?? "<null>"}'.")
        };
    }

    private static JsonSerializerOptions CreateFunctionResultJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class ThrowingAnalyticsSink : IProjectStructureAnalyticsService
    {
        public Task RecordAsync(
            ProjectStructureAnalyticsWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated analytics persistence failure.");
        }

        public Task<ProjectStructureAnalyticsResponse> QueryAsync(
            ProjectStructureAnalyticsQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class BlockingAnalyticsSink : IProjectStructureAnalyticsService
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken ObservedCancellationToken { get; private set; }

        public async Task RecordAsync(
            ProjectStructureAnalyticsWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedCancellationToken = cancellationToken;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }

        public Task<ProjectStructureAnalyticsResponse> QueryAsync(
            ProjectStructureAnalyticsQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
