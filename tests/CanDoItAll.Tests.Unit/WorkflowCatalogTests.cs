using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowCatalogTests
{
    [Fact]
    public async Task CatalogSavesDefinitionsAsNewVersions()
    {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var first = await catalog.SaveDefinitionAsync(CreateSaveRequest(
            CreateDefinitionGraph(component.Id),
            "Draft workflow"));
        var second = await catalog.SaveDefinitionAsync(CreateSaveRequest(
            CreateDefinitionGraph(component.Id),
            "Updated workflow",
            first.Id,
            first.VersionId));

        var list = await catalog.ListDefinitionsAsync();

        Assert.Single(list);
        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(first.VersionId, second.VersionId);
        Assert.Equal("Updated workflow", list[0].Name);
    }

    [Fact]
    public async Task ValidationCatchesDisconnectedNodesAndShapeMismatch()
    {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest() with
        {
            InputShape = WorkflowValueShape.Text,
            ResultShape = new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON")
        });
        var orphan = CreateNode("orphan", WorkflowNodeKind.StrictLogic, resultShape: WorkflowValueShape.Text);
        var definition = CreateDefinition(
            new WorkflowGraph(
                new WorkflowNodeId("start"),
                [
                    CreateNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                    CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
                    CreateNode("end", WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text),
                    orphan
                ],
                [
                    CreateEdge("start-to-llm", "start", "llm"),
                    CreateEdge("llm-to-end", "llm", "end")
                ]));

        var result = await catalog.ValidateDefinitionAsync(definition);

        Assert.Contains(result.Issues, issue => issue.Code == WorkflowValidationIssueCode.DisconnectedNode);
        Assert.Contains(result.Issues, issue => issue.Code == WorkflowValidationIssueCode.ShapeMismatch);
    }

    [Fact]
    public async Task ComponentLibraryRejectsUnsupportedModality()
    {
        var catalog = CreateCatalog();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.SaveComponentAsync(
            CreateComponentRequest() with { Modality = WorkflowModality.Audio }));

        Assert.Contains("modality", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ComponentLibraryListsChatProviderOptionsFromAgentProviderRegistry()
    {
        var chatProvider = CreateProvider(
            "OpenAI chat",
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.Chat,
            "gpt-5.4",
            ["gpt-5.4-mini"]);
        var imageProvider = CreateProvider(
            "OpenAI image",
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.ImageGeneration,
            "gpt-image-1",
            ["gpt-image-1"]);
        var catalog = CreateCatalog([chatProvider, imageProvider]);

        var options = await catalog.ListProviderOptionsAsync();

        var option = Assert.Single(options);
        Assert.Equal(chatProvider.Id, option.ProviderProfileId);
        Assert.Equal(ProviderProfilePurpose.Chat, option.Purpose);
        Assert.Contains("gpt-5.4", option.ModelOptions);
        Assert.Contains("gpt-5.4-mini", option.ModelOptions);
        Assert.True(option.SupportsStructuredOutput);
    }

    [Fact]
    public async Task ComponentLibraryRejectsNonChatProviderForLlmComponents()
    {
        var imageProvider = CreateProvider(
            "OpenAI image",
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.ImageGeneration,
            "gpt-image-1",
            ["gpt-image-1"]);
        var catalog = CreateCatalog([imageProvider]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.SaveComponentAsync(
            CreateComponentRequest() with
            {
                ProviderProfileId = imageProvider.Id,
                Model = imageProvider.DefaultModel
            }));

        Assert.Contains("not a chat provider", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ComponentLibraryRejectsStructuredOutputWhenProviderDoesNotSupportIt()
    {
        var ollamaProvider = CreateProvider(
            "Ollama chat",
            ProviderKind.Ollama,
            ProviderTransportKind.ChatCompletions,
            ProviderProfilePurpose.Chat,
            "llama3.2",
            ["llama3.2"]);
        var catalog = CreateCatalog([ollamaProvider]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.SaveComponentAsync(
            CreateComponentRequest() with
            {
                ProviderProfileId = ollamaProvider.Id,
                Model = ollamaProvider.DefaultModel,
                ModelSettings = new WorkflowModelSettings(
                    Temperature: 0.2,
                    MaxOutputTokens: 800,
                    RequireJsonOutput: true,
                    ResponseFormatJsonSchema: "{}")
            }));

        Assert.Contains("structured JSON output", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestRunnerExecutesSavedWorkflowWithInProcessBackend()
    {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var definition = await catalog.SaveDefinitionAsync(CreateSaveRequest(CreateDefinitionGraph(component.Id)));
        var runStore = new InMemoryWorkflowRunStore();
        var runner = CreateRunner(catalog, runStore);

        var result = await runner.RunAsync(new WorkflowTestRunRequest(
            definition.Id,
            definition.VersionId,
            DraftDefinition: null,
            "{\"prompt\":\"hello\"}",
            WorkflowRuntimeBackendKind.InProcess,
            ValidateOnly: false));

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotNull(result.Run);
        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
        Assert.NotEmpty(result.Events);
    }

    [Fact]
    public async Task TestRunnerReturnsValidationFailureWithoutStartingRun()
    {
        var catalog = CreateCatalog();
        var runner = CreateRunner(catalog, new InMemoryWorkflowRunStore());
        var definition = CreateDefinition(CreateDefinitionGraph(WorkflowComponentId.New()));

        var result = await runner.RunAsync(new WorkflowTestRunRequest(
            WorkflowId: null,
            VersionId: null,
            DraftDefinition: definition,
            InputJson: "{}",
            RequestedBackend: WorkflowRuntimeBackendKind.InProcess,
            ValidateOnly: false));

        Assert.False(result.Succeeded);
        Assert.Null(result.Run);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == WorkflowValidationIssueCode.InvalidComponentReference);
    }

    [Fact]
    public async Task TestRunnerReturnsRuntimeFailureForUnregisteredBackend()
    {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var definition = await catalog.SaveDefinitionAsync(CreateSaveRequest(
            CreateDefinitionGraph(component.Id),
            runtimePolicy: new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.DurableTask,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: true,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)));
        var runner = CreateRunner(catalog, new InMemoryWorkflowRunStore());

        var result = await runner.RunAsync(new WorkflowTestRunRequest(
            definition.Id,
            definition.VersionId,
            DraftDefinition: null,
            "{}",
            WorkflowRuntimeBackendKind.DurableTask,
            ValidateOnly: false));

        Assert.False(result.Succeeded);
        Assert.Contains("not registered", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static InMemoryWorkflowCatalogService CreateCatalog(IReadOnlyList<ProviderProfile>? providers = null)
    {
        var providerProfileService = providers is null ? null : new ProviderProfileService();
        return new InMemoryWorkflowCatalogService(
            new InMemoryWorkflowCatalogStore(),
            new WorkflowDefinitionValidator(),
            providers is null ? null : new TestProviderProfileRegistry(providers),
            providerProfileService);
    }

    private static WorkflowTestRunner CreateRunner(
        InMemoryWorkflowCatalogService catalog,
        InMemoryWorkflowRunStore runStore)
    {
        var runtimeManager = new WorkflowRuntimeManager(
            [
                new MafInProcessWorkflowExecutionBackend(
                    new MafWorkflowCompiler(new WorkflowDefinitionValidator()),
                    catalog)
            ],
            runStore);

        return new WorkflowTestRunner(catalog, runtimeManager, runStore);
    }

    private static WorkflowDefinitionSaveRequest CreateSaveRequest(
        WorkflowGraph graph,
        string name = "Sample workflow",
        WorkflowId? id = null,
        WorkflowVersionId? expectedVersionId = null,
        WorkflowRuntimePolicy? runtimePolicy = null)
    {
        return new WorkflowDefinitionSaveRequest(
            id,
            expectedVersionId,
            name,
            "Workflow for catalog tests.",
            WorkflowLifecycleStatus.Draft,
            graph,
            runtimePolicy ?? new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false));
    }

    private static WorkflowDefinition CreateDefinition(WorkflowGraph graph)
    {
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Draft workflow",
            "Workflow for catalog tests.",
            WorkflowLifecycleStatus.Draft,
            graph,
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static WorkflowGraph CreateDefinitionGraph(WorkflowComponentId componentId)
    {
        return new WorkflowGraph(
            new WorkflowNodeId("start"),
            [
                CreateNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                CreateNode("llm", WorkflowNodeKind.LlmCall, componentId),
                CreateNode("end", WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
            ],
            [
                CreateEdge("start-to-llm", "start", "llm"),
                CreateEdge("llm-to-end", "llm", "end")
            ]);
    }

    private static WorkflowNode CreateNode(
        string id,
        WorkflowNodeKind kind,
        WorkflowComponentId? componentId = null,
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null)
    {
        return new WorkflowNode(
            new WorkflowNodeId(id),
            kind,
            id,
            [],
            new WorkflowNodeSettings(
                componentId,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: inputShape ?? WorkflowValueShape.Text,
                ResultShape: resultShape ?? WorkflowValueShape.Text));
    }

    private static WorkflowEdge CreateEdge(string id, string source, string target)
    {
        return new WorkflowEdge(
            new WorkflowEdgeId(id),
            new WorkflowNodeId(source),
            SourcePortId: null,
            new WorkflowNodeId(target),
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty);
    }

    private static LlmCallComponentSaveRequest CreateComponentRequest()
    {
        return new LlmCallComponentSaveRequest(
            Id: null,
            Name: "Summarize",
            ProviderProfileId: null,
            Model: "gpt-5.4",
            Modality: WorkflowModality.Text,
            ModelSettings: new WorkflowModelSettings(
                Temperature: 0.2,
                MaxOutputTokens: 800,
                RequireJsonOutput: false,
                ResponseFormatJsonSchema: string.Empty),
            Instructions: "Summarize the input.",
            InputShape: WorkflowValueShape.Text,
            ResultShape: WorkflowValueShape.Text,
            Permissions: AgentPermissionsPolicy.Default);
    }

    private static ProviderProfile CreateProvider(
        string name,
        ProviderKind kind,
        ProviderTransportKind transport,
        ProviderProfilePurpose purpose,
        string defaultModel,
        IReadOnlyList<string> suggestedModels)
    {
        return new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: name,
            Kind: kind,
            BaseUrl: kind == ProviderKind.Ollama ? "http://localhost:11434" : "https://api.openai.com/v1",
            ApiKeyEnvironmentVariable: "TEST_PROVIDER_API_KEY",
            DefaultModel: defaultModel,
            Transport: transport,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: transport == ProviderTransportKind.Responses,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: suggestedModels,
            Purpose: purpose);
    }

    private sealed class TestProviderProfileRegistry(IReadOnlyList<ProviderProfile> providers) : IProviderProfileRegistry
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(providers);
        }

        public Task<ProviderProfile?> GetProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(providers.FirstOrDefault(provider => provider.Id == providerId));
        }

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(
            Guid? providerId = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Guid> SaveProviderAsync(
            ProviderProfileEditorModel model,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProviderProfile> UpdateProviderAsync(
            Guid providerId,
            Func<ProviderProfile, ProviderProfile> update,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
