using System.Collections.Concurrent;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowCatalogTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_refuses_empty_or_stale_original_and_removes_only_an_acknowledged_head(bool stale) {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var request = CreateSaveRequest(CreateDefinitionGraph(component.Id));
        var original = await catalog.SaveDefinitionAsync(request);
        var winner = await catalog.SaveDefinitionAsync(request with { Id = original.Id, ExpectedVersionId = original.VersionId, Name = "Peer winner" });
        await Assert.ThrowsAsync<WorkflowDefinitionConcurrencyException>(() => catalog.DeleteDefinitionAsync(
            new(original.Id, stale ? original.VersionId : default)));
        Assert.Equal(JsonSerializer.Serialize(winner), JsonSerializer.Serialize((await catalog.GetDefinitionAsync(original.Id))!.Definition));
        Assert.NotNull(await catalog.GetDefinitionAsync(original.Id, original.VersionId));
        await catalog.DeleteDefinitionAsync(new(original.Id, winner.VersionId));
        Assert.Null(await catalog.GetDefinitionAsync(original.Id));
        Assert.Null(await catalog.GetDefinitionAsync(original.Id, original.VersionId));
        Assert.Empty(await catalog.ListDefinitionsAsync());
    }

    [Theory]
    [InlineData(ImportOriginal.Missing)]
    [InlineData(ImportOriginal.Stale)]
    [InlineData(ImportOriginal.Current)]
    public async Task Import_preserves_the_winner_unless_its_original_version_is_supplied(ImportOriginal observation) {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var request = CreateSaveRequest(CreateDefinitionGraph(component.Id));
        var original = await catalog.SaveDefinitionAsync(request);
        var envelope = await catalog.ExportDefinitionAsync(original.Id);
        Assert.NotNull(envelope);
        var winner = await catalog.SaveDefinitionAsync(request with { Id = original.Id, ExpectedVersionId = original.VersionId, Name = "Peer winner" });
        var import = new WorkflowDefinitionImportRequest(envelope, "Reviewed import", null, true) {
            ExpectedVersionId = observation switch {
                ImportOriginal.Missing => null,
                ImportOriginal.Stale => original.VersionId,
                ImportOriginal.Current => winner.VersionId,
                _ => throw new ArgumentOutOfRangeException(nameof(observation))
            }
        };
        if (observation != ImportOriginal.Current) {
            await Assert.ThrowsAsync<WorkflowDefinitionConcurrencyException>(() => catalog.ImportDefinitionAsync(import));
            Assert.Equal(JsonSerializer.Serialize(winner), JsonSerializer.Serialize((await catalog.GetDefinitionAsync(original.Id))!.Definition));
            return;
        }
        var accepted = await catalog.ImportDefinitionAsync(import);
        Assert.Equal(original.Id, accepted.Id);
        Assert.NotEqual(winner.VersionId, accepted.VersionId);
        Assert.Equal("Reviewed import", accepted.Name);
        Assert.Equal(JsonSerializer.Serialize(original.Graph), JsonSerializer.Serialize(accepted.Graph));
        Assert.Equal(JsonSerializer.Serialize(accepted), JsonSerializer.Serialize((await catalog.GetDefinitionAsync(original.Id))!.Definition));
    }

    [Fact]
    public async Task Import_cannot_use_an_original_version_for_a_new_identity() {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var original = await catalog.SaveDefinitionAsync(CreateSaveRequest(CreateDefinitionGraph(component.Id)));
        var envelope = await catalog.ExportDefinitionAsync(original.Id);
        Assert.NotNull(envelope);
        await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.ImportDefinitionAsync(
            new(envelope, null, null, false) { ExpectedVersionId = original.VersionId }));
        Assert.Equal(original.VersionId, Assert.Single(await catalog.ListDefinitionsAsync()).VersionId);
    }

    [Fact]
    public async Task A_deleted_original_cannot_delete_a_recreated_workflow_identity() {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var request = CreateSaveRequest(CreateDefinitionGraph(component.Id));
        var original = await catalog.SaveDefinitionAsync(request);
        await catalog.DeleteDefinitionAsync(new(original.Id, original.VersionId));
        var recreated = await catalog.SaveDefinitionAsync(request with { Id = original.Id, Name = "Recreated head" });
        await Assert.ThrowsAsync<WorkflowDefinitionConcurrencyException>(() => catalog.DeleteDefinitionAsync(new(original.Id, original.VersionId)));
        Assert.Equal(JsonSerializer.Serialize(recreated), JsonSerializer.Serialize((await catalog.GetDefinitionAsync(original.Id))!.Definition));
    }

    public enum ImportOriginal { Missing, Stale, Current }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Existing_definition_save_and_status_require_the_original_version(bool statusOnly, bool peerCommitted) {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var request = CreateSaveRequest(CreateDefinitionGraph(component.Id), "Original definition");
        var original = await catalog.SaveDefinitionAsync(request);
        var winner = peerCommitted
            ? await catalog.SaveDefinitionAsync(request with { Id = original.Id, ExpectedVersionId = original.VersionId, Name = "Peer winner" })
            : original;

        Task<WorkflowDefinition> Submit() => statusOnly
            ? catalog.ChangeDefinitionStatusAsync(new(original.Id, null, WorkflowLifecycleStatus.Suspended))
            : catalog.SaveDefinitionAsync(request with { Id = original.Id, ExpectedVersionId = null, Name = "Held replacement" });

        await Assert.ThrowsAsync<WorkflowDefinitionConcurrencyException>(Submit);
        var current = await catalog.GetDefinitionAsync(original.Id);
        Assert.NotNull(current);
        Assert.Equal(JsonSerializer.Serialize(winner), JsonSerializer.Serialize(current.Definition));
    }

    [Fact]
    public async Task Chosen_definition_id_is_create_only_without_an_original_version() {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var chosenId = WorkflowId.New();
        var request = CreateSaveRequest(CreateDefinitionGraph(component.Id), "Chosen original", chosenId);
        var original = await catalog.SaveDefinitionAsync(request);

        Assert.Equal(chosenId, original.Id);
        await Assert.ThrowsAsync<WorkflowDefinitionConcurrencyException>(() =>
            catalog.SaveDefinitionAsync(request with { Name = "Colliding creator" }));
        Assert.Equal(original.VersionId, (await catalog.GetDefinitionAsync(chosenId))!.Definition.VersionId);
    }

    [Fact]
    public async Task An_original_version_cannot_create_a_missing_definition() {
        var catalog = CreateCatalog();
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var missingId = WorkflowId.New();

        await Assert.ThrowsAsync<WorkflowDefinitionConcurrencyException>(() =>
            catalog.SaveDefinitionAsync(CreateSaveRequest(CreateDefinitionGraph(component.Id), "Missing original", missingId, WorkflowVersionId.New())));
        Assert.Null(await catalog.GetDefinitionAsync(missingId));
        Assert.Empty(await catalog.ListDefinitionsAsync());
    }

    [Fact]
    public async Task Concurrent_chosen_id_creators_append_exactly_one_version_after_validation() {
        using var validator = new ConcurrentDefinitionValidator();
        var store = new InMemoryWorkflowCatalogStore();
        var catalog = new InMemoryWorkflowCatalogService(store, validator);
        var reader = new InMemoryWorkflowCatalogService(store, new WorkflowDefinitionValidator());
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        validator.Synchronize = true;
        var chosenId = WorkflowId.New();
        var request = CreateSaveRequest(CreateDefinitionGraph(component.Id), id: chosenId);

        async Task<(WorkflowDefinition? Saved, Exception? Error)> SubmitAsync(string name) {
            try {
                return (await catalog.SaveDefinitionAsync(request with { Name = name }), null);
            } catch (Exception error) {
                return (null, error);
            }
        }

        var results = await Task.WhenAll(
            Task.Run(() => SubmitAsync("First creator")),
            Task.Run(() => SubmitAsync("Second creator")));
        var winner = Assert.Single(results, result => result.Saved is not null).Saved!;
        Assert.IsType<WorkflowDefinitionConcurrencyException>(Assert.Single(results, result => result.Error is not null).Error);
        Assert.Equal(winner.VersionId, (await reader.GetDefinitionAsync(chosenId))!.Definition.VersionId);
        var persisted = new List<WorkflowDefinitionDetail>();
        foreach (var version in validator.Versions) {
            var detail = await reader.GetDefinitionAsync(chosenId, version);
            if (detail is not null) {
                persisted.Add(detail);
            }
        }
        Assert.Equal(winner.VersionId, Assert.Single(persisted).Definition.VersionId);
    }

    [Fact]
    public async Task Deletion_during_validation_cannot_resurrect_the_held_replacement() {
        using var validator = new HeldDefinitionValidator();
        var catalog = new InMemoryWorkflowCatalogService(new InMemoryWorkflowCatalogStore(), validator);
        var component = await catalog.SaveComponentAsync(CreateComponentRequest());
        var request = CreateSaveRequest(CreateDefinitionGraph(component.Id));
        var original = await catalog.SaveDefinitionAsync(request);
        validator.Hold = true;
        var pending = Task.Run(() => catalog.SaveDefinitionAsync(request with {
            Id = original.Id,
            ExpectedVersionId = original.VersionId,
            Name = "Held replacement"
        }));
        try {
            await validator.Entered.WaitAsync(TimeSpan.FromSeconds(15));
            await catalog.DeleteDefinitionAsync(new(original.Id, original.VersionId));
        } finally {
            validator.Continue();
        }

        await Assert.ThrowsAsync<WorkflowDefinitionConcurrencyException>(() => pending);
        Assert.Null(await catalog.GetDefinitionAsync(original.Id));
        Assert.Empty(await catalog.ListDefinitionsAsync());
    }

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
            ["gpt-5.4-mini"]) with {
            CredentialBinding = new(Guid.NewGuid(), ProviderCredentialPurpose.SourceAccessToken,
                ProviderCredentialConsumerKind.Source, Guid.NewGuid()),
            ModelCatalog = [new("gpt-5.4", "Exact default / custom"), new("gpt-5.4-mini", "Exact second / custom")]
        };
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
        Assert.True(option.IsSourceManaged);
        Assert.Equal(chatProvider.ModelCatalog, option.ModelCatalog);
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
    public async Task ComponentLibraryAcceptsStructuredOutputForOllama()
    {
        var ollamaProvider = CreateProvider(
            "Ollama chat",
            ProviderKind.Ollama,
            ProviderTransportKind.ChatCompletions,
            ProviderProfilePurpose.Chat,
            "llama3.2",
            ["llama3.2"]) with {
                ModelPrices = [new ProviderModelTokenPrice("llama3.2", 0m, 0m, 0m) {
                    TariffKind = ProviderTariffKind.ExplicitFree
                }]
            };
        var catalog = CreateCatalog([ollamaProvider]);

        var component = await catalog.SaveComponentAsync(
            CreateComponentRequest() with
            {
                ProviderProfileId = ollamaProvider.Id,
                Model = ollamaProvider.DefaultModel,
                ModelSettings = new WorkflowModelSettings(
                    Temperature: 0.2,
                    MaxOutputTokens: 800,
                    RequireJsonOutput: true,
                    ResponseFormatJsonSchema: "{}")
            });

        Assert.Equal(ollamaProvider.Id, component.ProviderProfileId);
        Assert.True(component.ModelSettings.RequireJsonOutput);
        Assert.Equal("{}", component.ModelSettings.ResponseFormatJsonSchema);
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
        var runtimeManager = WorkflowRuntimeManager.CreateInMemory(
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
        var runtimeManager = WorkflowRuntimeManager.CreateInMemory(
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
            new WorkflowLaunchTestAuthorizationScopeResolver(),
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

    private sealed class ConcurrentDefinitionValidator : IWorkflowDefinitionValidator, IDisposable {
        private readonly Barrier barrier = new(2);
        private readonly WorkflowDefinitionValidator inner = new();
        public ConcurrentBag<WorkflowVersionId> Versions { get; } = new();
        public bool Synchronize { get; set; }

        public WorkflowValidationResult Validate(WorkflowDefinition definition, IReadOnlyList<LlmCallComponent> components) {
            if (Synchronize) {
                Versions.Add(definition.VersionId);
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15))) {
                    throw new TimeoutException("Concurrent creators did not reach validation together.");
                }
            }
            return inner.Validate(definition, components);
        }

        public void Dispose() => barrier.Dispose();
    }

    private sealed class HeldDefinitionValidator : IWorkflowDefinitionValidator, IDisposable {
        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim release = new(false);
        private readonly WorkflowDefinitionValidator inner = new();
        public bool Hold { get; set; }
        public Task Entered => entered.Task;

        public WorkflowValidationResult Validate(WorkflowDefinition definition, IReadOnlyList<LlmCallComponent> components) {
            if (Hold) {
                entered.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(15))) {
                    throw new TimeoutException("Held definition validation was not released.");
                }
            }
            return inner.Validate(definition, components);
        }

        public void Continue() => release.Set();
        public void Dispose() => release.Dispose();
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
