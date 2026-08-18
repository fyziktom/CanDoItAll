using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowCatalogTests
{
    [Theory]
    [InlineData(-1, 25)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    [InlineData(int.MaxValue, 2)]
    public void CatalogSearchQueryRejectsInvalidOrOverflowingPageBounds(int pageIndex, int pageSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkflowCatalogSearchQuery(pageIndex: pageIndex, pageSize: pageSize));
    }

    [Fact]
    public async Task CatalogSearchFiltersCountsAndPagesLatestDefinitions()
    {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var graph = CreateDefinitionGraph(component.Id);
        var oldest = await catalog.SaveDefinitionAsync(CreateSaveRequest(graph, "Needle oldest"));
        await catalog.SaveDefinitionAsync(CreateSaveRequest(graph, "Unrelated draft"));
        await catalog.SaveDefinitionAsync(CreateSaveRequest(graph, "Needle active") with
        {
            Status = WorkflowLifecycleStatus.Active
        });
        var newest = await catalog.SaveDefinitionAsync(CreateSaveRequest(graph, "needle newest"));

        var firstPage = await catalog.SearchDefinitionsAsync(new WorkflowCatalogSearchQuery(
            "  NEEDLE  ",
            WorkflowLifecycleStatus.Draft,
            pageIndex: 0,
            pageSize: 1));
        var secondPage = await catalog.SearchDefinitionsAsync(new WorkflowCatalogSearchQuery(
            "needle",
            WorkflowLifecycleStatus.Draft,
            pageIndex: 1,
            pageSize: 1));
        var beyondLastPage = await catalog.SearchDefinitionsAsync(new WorkflowCatalogSearchQuery(
            "needle",
            WorkflowLifecycleStatus.Draft,
            pageIndex: 2,
            pageSize: 1));

        Assert.Equal(2, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(newest.Id, Assert.Single(firstPage.Items).Id);
        Assert.Equal(oldest.Id, Assert.Single(secondPage.Items).Id);
        Assert.Equal(2, beyondLastPage.TotalCount);
        Assert.Empty(beyondLastPage.Items);
    }

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
    public async Task CatalogSnapshotsDefinitionGraphOnSave()
    {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var nodes = CreateDefinitionGraph(component.Id).Nodes.ToList();
        var edges = CreateDefinitionGraph(component.Id).Edges.ToList();
        var graph = new WorkflowGraph(new WorkflowNodeId("start"), nodes, edges);

        var saved = await catalog.SaveDefinitionAsync(CreateSaveRequest(graph));
        nodes.Clear();
        edges.Clear();
        var detail = await catalog.GetDefinitionAsync(saved.Id);

        Assert.NotNull(detail);
        Assert.Equal(3, detail.Definition.Graph.Nodes.Count);
        Assert.Equal(2, detail.Definition.Graph.Edges.Count);
    }

    [Fact]
    public async Task CatalogSnapshotsBlankLlmNodeInstructionsFromComponentOnSave()
    {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var graph = CreateDefinitionGraph(component.Id);
        var draftNode = Assert.Single(graph.Nodes, node => node.Kind == WorkflowNodeKind.LlmCall);
        Assert.True(string.IsNullOrWhiteSpace(draftNode.Settings.Instructions));

        var saved = await catalog.SaveDefinitionAsync(CreateSaveRequest(graph));
        var detail = await catalog.GetDefinitionAsync(saved.Id, saved.VersionId);

        var savedNode = Assert.Single(
            detail!.Definition.Graph.Nodes,
            node => node.Kind == WorkflowNodeKind.LlmCall);
        Assert.Equal(component.Instructions, savedNode.Settings.Instructions);
        Assert.True(string.IsNullOrWhiteSpace(draftNode.Settings.Instructions));
    }

    [Fact]
    public async Task CatalogPersistsNodeProviderModelOverridesWithoutMutatingSharedComponent()
    {
        var componentProvider = CreateProvider(
            "Component provider",
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.Chat,
            "component-model",
            []) with
        {
            ModelPrices = [new ProviderModelTokenPrice("component-model", 1m, 0.1m, 2m)]
        };
        var nodeProvider = CreateProvider(
            "Node provider",
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.Chat,
            "node-model",
            []) with
        {
            ModelPrices = [new ProviderModelTokenPrice("node-model", 1m, 0.1m, 2m)]
        };
        var catalog = CreateCatalog([componentProvider, nodeProvider]);
        var component = await catalog.SaveComponentAsync(CreateComponentRequest() with
        {
            ProviderProfileId = componentProvider.Id,
            Model = componentProvider.DefaultModel
        });
        var graph = CreateDefinitionGraph(component.Id);
        var nodes = graph.Nodes
            .Select(node => node.Kind == WorkflowNodeKind.LlmCall
                ? node with
                {
                    Settings = node.Settings with
                    {
                        ProviderProfileId = nodeProvider.Id,
                        Model = nodeProvider.DefaultModel
                    }
                }
                : node)
            .ToArray();

        var saved = await catalog.SaveDefinitionAsync(CreateSaveRequest(new WorkflowGraph(
            graph.StartNodeId,
            nodes,
            graph.Edges)));
        var detail = await catalog.GetDefinitionAsync(saved.Id, saved.VersionId);
        var persistedComponent = await catalog.GetComponentAsync(component.Id);

        var savedNode = Assert.Single(detail!.Definition.Graph.Nodes, node => node.Kind == WorkflowNodeKind.LlmCall);
        Assert.Equal(nodeProvider.Id, savedNode.Settings.ProviderProfileId);
        Assert.Equal(nodeProvider.DefaultModel, savedNode.Settings.Model);
        Assert.Equal(componentProvider.Id, persistedComponent!.ProviderProfileId);
        Assert.Equal(componentProvider.DefaultModel, persistedComponent.Model);
    }

    [Fact]
    public async Task CatalogRejectsIncompatibleNodeModelOverride()
    {
        var provider = CreateProvider(
            "Priced provider",
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.Chat,
            "priced-model",
            []) with
        {
            ModelPrices = [new ProviderModelTokenPrice("priced-model", 1m, 0.1m, 2m)]
        };
        var catalog = CreateCatalog([provider]);
        var component = await catalog.SaveComponentAsync(CreateComponentRequest() with
        {
            ProviderProfileId = provider.Id,
            Model = provider.DefaultModel
        });
        var graph = CreateDefinitionGraph(component.Id);
        var nodes = graph.Nodes
            .Select(node => node.Kind == WorkflowNodeKind.LlmCall
                ? node with
                {
                    Settings = node.Settings with
                    {
                        ProviderProfileId = provider.Id,
                        Model = "unpriced-model"
                    }
                }
                : node)
            .ToArray();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            catalog.SaveDefinitionAsync(CreateSaveRequest(new WorkflowGraph(
                graph.StartNodeId,
                nodes,
                graph.Edges))));

        Assert.Contains("unpriced-model", exception.Message, StringComparison.Ordinal);
        Assert.Contains("model price row", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CatalogSnapshotsTypedRouteMetadataOnEdges()
    {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var graph = CreateDefinitionGraph(component.Id);
        var routedEdges = graph.Edges
            .Select(edge => edge.Id.Value == "start-to-llm"
                ? edge with
                {
                    Kind = WorkflowEdgeKind.Conditional,
                    Routing = WorkflowEdgeRouting.Predicate(
                        "$.invoice.total",
                        WorkflowRouteOperator.GreaterThanOrEqual,
                        "5000",
                        WorkflowRouteValueKind.Number,
                        "High value")
                }
                : edge)
            .ToArray();

        var saved = await catalog.SaveDefinitionAsync(CreateSaveRequest(new WorkflowGraph(
            graph.StartNodeId,
            graph.Nodes,
            routedEdges)));
        var detail = await catalog.GetDefinitionAsync(saved.Id);

        var routedEdge = Assert.Single(detail!.Definition.Graph.Edges, edge => edge.Routing.Kind == WorkflowRouteKind.Predicate);
        Assert.Equal("High value", routedEdge.Routing.Label);
        Assert.Equal("$.invoice.total", routedEdge.Routing.JsonPath);
        Assert.Equal(WorkflowRouteOperator.GreaterThanOrEqual, routedEdge.Routing.Operator);
        Assert.Equal(WorkflowRouteValueKind.Number, routedEdge.Routing.ExpectedValueKind);
        Assert.Equal("5000", routedEdge.Routing.ExpectedValueJson);
    }

    [Fact]
    public async Task CatalogPreservesWorkflowInputParametersOnSaveAndStatusChange()
    {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var saved = await catalog.SaveDefinitionAsync(CreateSaveRequest(CreateDefinitionGraph(component.Id)) with
        {
            InputParameters = CreateWorkflowInputParameters()
        });

        var active = await catalog.ChangeDefinitionStatusAsync(new WorkflowDefinitionStatusChangeRequest(
            saved.Id,
            saved.VersionId,
            WorkflowLifecycleStatus.Active));
        var detail = await catalog.GetDefinitionAsync(active.Id, active.VersionId);

        var email = Assert.Single(detail!.Definition.InputParameters, parameter => parameter.Key == "emailAddress");
        Assert.Equal(WorkflowInputParameterKind.EmailAddress, email.Kind);
        Assert.Equal(WorkflowInputParameterOptionSourceKind.CrmContacts, email.OptionSource.Kind);
        Assert.Equal("$.emailAddress", email.JsonPath);
    }

    [Fact]
    public async Task CatalogRejectsInvalidDefinitionOnSave()
    {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var graph = CreateDefinitionGraph(component.Id);
        var invalidGraph = new WorkflowGraph(
            graph.StartNodeId,
            graph.Nodes,
            graph.Edges.Append(CreateEdge("start-missing", "start", "missing-node")).ToArray());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            catalog.SaveDefinitionAsync(CreateSaveRequest(invalidGraph)));

        Assert.Contains("save failed validation", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("start-missing", exception.Message, StringComparison.OrdinalIgnoreCase);
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
    public async Task TestRunnerReturnsValidationFailureForUnavailableBackendPolicy()
    {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var definition = CreateDefinition(CreateDefinitionGraph(component.Id)) with
        {
            RuntimePolicy = new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.DurableTask,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: true,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false)
        };
        var runner = CreateRunner(catalog, new InMemoryWorkflowRunStore());

        var result = await runner.RunAsync(new WorkflowTestRunRequest(
            WorkflowId: null,
            VersionId: null,
            DraftDefinition: definition,
            "{}",
            WorkflowRuntimeBackendKind.DurableTask,
            ValidateOnly: false));

        Assert.False(result.Succeeded);
        Assert.Null(result.Run);
        var issue = Assert.Single(result.Validation.Issues, issue => issue.Code == WorkflowValidationIssueCode.UnsupportedRuntimeBackend);
        Assert.Contains("not registered", issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CatalogRejectsUnavailableProductionBackendOnSave()
    {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            catalog.SaveDefinitionAsync(CreateSaveRequest(
                CreateDefinitionGraph(component.Id),
                runtimePolicy: new WorkflowRuntimePolicy(
                    WorkflowRuntimeBackendKind.DurableTask,
                    AllowInProcessPreviewRuns: true,
                    RequireDurableProductionRuns: true,
                    ExposeAzureFunctionsStatusEndpoint: false,
                    ExposeAzureFunctionsMcpTool: false))));

        Assert.Contains("not registered", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nameof(WorkflowRuntimeBackendKind.DurableTask), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeManagerRejectsInProcessWhenDurablePolicyDisallowsPreview()
    {
        var runStore = new InMemoryWorkflowRunStore();
        var runtimeManager = new WorkflowRuntimeManager(
            [
                new MafInProcessWorkflowExecutionBackend(
                    new MafWorkflowCompiler(new WorkflowDefinitionValidator()),
                    [])
            ],
            runStore);
        var definition = CreateDefinition(new WorkflowGraph(
            new WorkflowNodeId("start"),
            [
                CreateNode("start", WorkflowNodeKind.Start),
                CreateNode("end", WorkflowNodeKind.End)
            ],
            [CreateEdge("start-end", "start", "end")])) with
            {
                RuntimePolicy = new WorkflowRuntimePolicy(
                    WorkflowRuntimeBackendKind.DurableTask,
                    AllowInProcessPreviewRuns: false,
                    RequireDurableProductionRuns: true,
                    ExposeAzureFunctionsStatusEndpoint: false,
                    ExposeAzureFunctionsMcpTool: false)
            };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtimeManager.StartAsync(
                definition,
                new WorkflowRunStartRequest(
                    definition.Id,
                    definition.VersionId,
                    "{}",
                    WorkflowRuntimeBackendKind.InProcess,
                    SourceProcessRunId: null,
                    SourceProcessAssignmentId: null)));

        Assert.Contains("requires a durable production runtime", exception.Message, StringComparison.OrdinalIgnoreCase);
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
                    new MafWorkflowCompiler(
                        new WorkflowDefinitionValidator(),
                        llmComponentInvoker: new PassthroughLlmComponentInvoker()),
                    catalog)
            ],
            runStore);

        var launchService = new WorkflowLaunchService(
            catalog,
            new WorkflowRuntimeBackendCatalog([WorkflowRuntimeBackendKind.InProcess]),
            new WorkflowRuntimeManagerRunLauncher(runtimeManager),
            new InMemoryWorkflowLaunchIdempotencyStore(),
            runStore,
            TimeProvider.System);
        return new WorkflowTestRunner(catalog, launchService, runtimeManager, runStore);
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

    private static IReadOnlyList<WorkflowInputParameterDescriptor> CreateWorkflowInputParameters()
    {
        return
        [
            new WorkflowInputParameterDescriptor(
                "emailAddress",
                "Email address",
                WorkflowInputParameterKind.EmailAddress,
                IsRequired: true,
                "Watched sender address.",
                "$.emailAddress",
                DefaultValue: string.Empty,
                new WorkflowInputParameterOptionSource(
                    WorkflowInputParameterOptionSourceKind.CrmContacts,
                    DependsOnParameterKey: string.Empty,
                    StaticOptions: []),
                MinimumValue: null,
                MaximumValue: null,
                Placeholder: string.Empty)
        ];
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

    private sealed class TestProviderProfileRegistry(IReadOnlyList<ProviderProfile> providers) :
        IProviderProfileRegistry,
        IProviderRuntimeProfileSource
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

    private sealed class PassthroughLlmComponentInvoker : IWorkflowLlmComponentInvoker
    {
        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowDefinition definition,
            WorkflowNode node,
            LlmCallComponent component,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                node.Id,
                input.PayloadJson,
                component.ResultShape));
        }
    }
}
