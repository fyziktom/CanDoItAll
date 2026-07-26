using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowStableIdentityTests
{
    private const string SourceHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public async Task Template_lookup_normalizes_exact_key_and_pins_latest_active_version()
    {
        var (catalog, lookup) = CreateServices();
        var graph = await CreateGraphAsync(catalog);
        var draft = await catalog.SaveDefinitionAsync(CreateSaveRequest(graph, "Original display label") with
        {
            TemplateProvenance = CreateTemplateProvenance("  Billing.Review  ")
        });
        var active = await ChangeStatusAsync(catalog, draft, WorkflowLifecycleStatus.Active);
        var laterDraft = await catalog.SaveDefinitionAsync(CreateSaveRequest(
            graph,
            "A display label unrelated to the template key",
            active.Id,
            active.VersionId));

        var result = await lookup.ResolveByTemplateKeyAsync("  BILLING.REVIEW  ");

        Assert.Equal(WorkflowStableIdentityKind.Template, result.IdentityKind);
        Assert.Equal(string.Empty, result.Namespace);
        Assert.Equal("billing.review", result.Key);
        Assert.Equal(WorkflowStableIdentityResolutionStatus.Resolved, result.Status);
        Assert.Equal(laterDraft.Id, result.WorkflowId);
        Assert.Equal(active.VersionId, result.RunnableVersionId);
        var materialization = Assert.Single(result.Materializations);
        Assert.Equal(laterDraft.VersionId, materialization.VersionId);
        Assert.Equal(laterDraft.Name, materialization.Name);
        Assert.Equal("billing.review", materialization.TemplateKey);
    }

    [Fact]
    public async Task Template_lookup_returns_not_found_for_missing_exact_key()
    {
        var (_, lookup) = CreateServices();

        var result = await lookup.ResolveByTemplateKeyAsync("missing.workflow");

        Assert.Equal(WorkflowStableIdentityResolutionStatus.NotFound, result.Status);
        Assert.Null(result.WorkflowId);
        Assert.Null(result.RunnableVersionId);
        Assert.Empty(result.Materializations);
    }

    [Fact]
    public async Task Template_lookup_returns_ambiguous_for_duplicate_materializations()
    {
        var (catalog, lookup) = CreateServices();
        var graph = await CreateGraphAsync(catalog);
        await catalog.SaveDefinitionAsync(CreateSaveRequest(graph, "First materialization") with
        {
            TemplateProvenance = CreateTemplateProvenance("shared.template")
        });
        await catalog.SaveDefinitionAsync(CreateSaveRequest(graph, "Second materialization") with
        {
            TemplateProvenance = CreateTemplateProvenance("SHARED.TEMPLATE")
        });

        var result = await lookup.ResolveByTemplateKeyAsync("shared.template");

        Assert.Equal(WorkflowStableIdentityResolutionStatus.Ambiguous, result.Status);
        Assert.Null(result.WorkflowId);
        Assert.Null(result.RunnableVersionId);
        Assert.Equal(2, result.Materializations.Count);
        Assert.All(result.Materializations, item => Assert.Equal("shared.template", item.TemplateKey));
    }

    [Theory]
    [InlineData(WorkflowLifecycleStatus.Suspended)]
    [InlineData(WorkflowLifecycleStatus.Archived)]
    public async Task Template_lookup_returns_stale_after_current_materialization_stops_running(
        WorkflowLifecycleStatus terminalStatus)
    {
        var (catalog, lookup) = CreateServices();
        var graph = await CreateGraphAsync(catalog);
        var draft = await catalog.SaveDefinitionAsync(CreateSaveRequest(graph) with
        {
            TemplateProvenance = CreateTemplateProvenance("lifecycle.template")
        });
        var active = await ChangeStatusAsync(catalog, draft, WorkflowLifecycleStatus.Active);
        var terminal = await ChangeStatusAsync(catalog, active, terminalStatus);

        var result = await lookup.ResolveByTemplateKeyAsync("lifecycle.template");

        Assert.Equal(WorkflowStableIdentityResolutionStatus.Stale, result.Status);
        Assert.Equal(terminal.Id, result.WorkflowId);
        Assert.Null(result.RunnableVersionId);
        Assert.Equal(terminalStatus, Assert.Single(result.Materializations).Status);
    }

    [Fact]
    public async Task External_lookup_normalizes_namespace_and_key()
    {
        var (catalog, lookup) = CreateServices();
        var graph = await CreateGraphAsync(catalog);
        var draft = await catalog.SaveDefinitionAsync(CreateSaveRequest(graph) with
        {
            ExternalNamespace = "  PARTNER.System  ",
            ExternalKey = "  Invoice:Review  "
        });
        var active = await ChangeStatusAsync(catalog, draft, WorkflowLifecycleStatus.Active);

        var result = await lookup.ResolveByExternalKeyAsync(
            " Partner.SYSTEM ",
            " INVOICE:REVIEW ");

        Assert.Equal(WorkflowStableIdentityKind.External, result.IdentityKind);
        Assert.Equal("partner.system", result.Namespace);
        Assert.Equal("invoice:review", result.Key);
        Assert.Equal(WorkflowStableIdentityResolutionStatus.Resolved, result.Status);
        Assert.Equal(active.Id, result.WorkflowId);
        Assert.Equal(active.VersionId, result.RunnableVersionId);
        var materialization = Assert.Single(result.Materializations);
        Assert.Equal("partner.system", materialization.ExternalNamespace);
        Assert.Equal("invoice:review", materialization.ExternalKey);
    }

    [Fact]
    public async Task Catalog_rejects_duplicate_normalized_external_binding()
    {
        var (catalog, _) = CreateServices();
        var graph = await CreateGraphAsync(catalog);
        var first = await catalog.SaveDefinitionAsync(CreateSaveRequest(graph, "First binding") with
        {
            ExternalNamespace = "partner.system",
            ExternalKey = "invoice:review"
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            catalog.SaveDefinitionAsync(CreateSaveRequest(graph, "Conflicting binding") with
            {
                ExternalNamespace = " PARTNER.SYSTEM ",
                ExternalKey = " INVOICE:REVIEW "
            }));

        Assert.Contains("already bound", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first.Id.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Catalog_and_detail_project_normalized_provenance_fields()
    {
        var (catalog, _) = CreateServices();
        var graph = await CreateGraphAsync(catalog);
        var saved = await catalog.SaveDefinitionAsync(CreateSaveRequest(graph, "Provenance projection") with
        {
            TemplateProvenance = new WorkflowTemplateProvenance(
                "  Pack.Workflow  ",
                "  Standard.Pack  ",
                "  2026.07  ",
                SourceHash),
            ExternalNamespace = "  CONNECTOR.System  ",
            ExternalKey = "  Case:Open  "
        });

        var detail = await catalog.GetDefinitionAsync(saved.Id);
        var catalogItem = Assert.Single(await catalog.ListDefinitionsAsync());

        Assert.NotNull(detail);
        AssertProvenance(detail!.Definition);
        Assert.Equal("pack.workflow", catalogItem.TemplateKey);
        Assert.Equal("standard.pack", catalogItem.TemplatePackKey);
        Assert.Equal("2026.07", catalogItem.TemplatePackVersion);
        Assert.Equal(SourceHash.ToLowerInvariant(), catalogItem.SourceHash);
        Assert.Equal("connector.system", catalogItem.ExternalNamespace);
        Assert.Equal("case:open", catalogItem.ExternalKey);
    }

    private static void AssertProvenance(WorkflowDefinition definition)
    {
        Assert.Equal("pack.workflow", definition.TemplateKey);
        Assert.Equal("standard.pack", definition.TemplatePackKey);
        Assert.Equal("2026.07", definition.TemplatePackVersion);
        Assert.Equal(SourceHash.ToLowerInvariant(), definition.SourceHash);
        Assert.Equal("connector.system", definition.ExternalNamespace);
        Assert.Equal("case:open", definition.ExternalKey);
    }

    private static (InMemoryWorkflowCatalogService Catalog, WorkflowStableIdentityLookupService Lookup)
        CreateServices()
    {
        var store = new InMemoryWorkflowCatalogStore();
        var catalog = new InMemoryWorkflowCatalogService(store, new WorkflowDefinitionValidator());
        return (
            catalog,
            new WorkflowStableIdentityLookupService(catalog));
    }

    private static async Task<WorkflowGraph> CreateGraphAsync(InMemoryWorkflowCatalogService catalog)
    {
        var component = await catalog.SaveComponentAsync(new LlmCallComponentSaveRequest(
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
            Permissions: AgentPermissionsPolicy.Default));

        return new WorkflowGraph(
            new WorkflowNodeId("start"),
            [
                CreateNode("start", WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                CreateNode("llm", WorkflowNodeKind.LlmCall, component.Id),
                CreateNode("end", WorkflowNodeKind.End, inputShape: WorkflowValueShape.Text)
            ],
            [
                CreateEdge("start-to-llm", "start", "llm"),
                CreateEdge("llm-to-end", "llm", "end")
            ]);
    }

    private static WorkflowDefinitionSaveRequest CreateSaveRequest(
        WorkflowGraph graph,
        string name = "Stable identity workflow",
        WorkflowId? id = null,
        WorkflowVersionId? expectedVersionId = null)
        => new(
            id,
            expectedVersionId,
            name,
            "Workflow stable identity test definition.",
            WorkflowLifecycleStatus.Draft,
            graph,
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false));

    private static WorkflowTemplateProvenance CreateTemplateProvenance(string key)
        => new(key, "stable.pack", "2026.07", SourceHash);

    private static Task<WorkflowDefinition> ChangeStatusAsync(
        InMemoryWorkflowCatalogService catalog,
        WorkflowDefinition definition,
        WorkflowLifecycleStatus status)
        => catalog.ChangeDefinitionStatusAsync(new WorkflowDefinitionStatusChangeRequest(
            definition.Id,
            definition.VersionId,
            status));

    private static WorkflowNode CreateNode(
        string id,
        WorkflowNodeKind kind,
        WorkflowComponentId? componentId = null,
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null)
        => new(
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

    private static WorkflowEdge CreateEdge(string id, string source, string target)
        => new(
            new WorkflowEdgeId(id),
            new WorkflowNodeId(source),
            SourcePortId: null,
            new WorkflowNodeId(target),
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty);
}
