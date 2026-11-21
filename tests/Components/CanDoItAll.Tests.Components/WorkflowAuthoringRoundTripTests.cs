using CanDoItAll.Modules.AgentFramework;
using System.Text.Json;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.Modules.AgentFramework.Pages.Authoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

[Trait("Category", "HostPlatform")]
public sealed class WorkflowAuthoringRoundTripTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_unchanged_and_node_name_edits_preserve_the_complete_supported_document(bool editNode) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var catalog = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var original = await SaveAndReadAsync(catalog, CreateRequest());
        WorkflowDefinition? accepted = null;
        var cut = harness.Context.Render<WorkflowCanvasEditor>(p => p.Add(x => x.Authorize, (_, _) => ValueTask.CompletedTask).Add(x => x.Definition, original)
            .Add(x => x.DefinitionSaved, value => accepted = value));
        cut.WaitForElement("[data-testid='workflow-canvas-save']", TimeSpan.FromSeconds(10));
        if (editNode) {
            await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-tab-node']").ClickAsync());
            await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-node-name']").Change("Edited start"));
        }
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        var receipt = Assert.IsType<WorkflowDefinition>(accepted);
        await using var scope = harness.Context.Services.CreateAsyncScope();
        var actual = Assert.IsType<WorkflowDefinitionDetail>(await scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>()
            .GetDefinitionAsync(receipt.Id, receipt.VersionId)).Definition;
        var expected = original with { VersionId = actual.VersionId, UpdatedAtUtc = actual.UpdatedAtUtc,
            CreatedAtUtc = ToPostgreSqlPrecision(original.CreatedAtUtc), Graph = original.Graph with {
                Nodes = original.Graph.Nodes.Select(node => editNode && node.Id == original.Graph.StartNodeId
                    ? node with { Name = "Edited start" } : node).ToArray()
            } };
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(
            Assert.IsType<WorkflowDefinitionDetail>(await catalog.GetDefinitionAsync(original.Id, original.VersionId)).Definition));
    }

    [Fact]
    public async Task Native_competing_save_rejects_the_exact_stale_version_and_retains_the_losing_draft() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var catalog = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var original = await SaveAndReadAsync(catalog, CreateRequest());
        var cut = harness.Context.Render<WorkflowCanvasEditor>(p => p.Add(x => x.Authorize, (_, _) => ValueTask.CompletedTask).Add(x => x.Definition, original));
        cut.WaitForElement("[data-testid='workflow-canvas-save']", TimeSpan.FromSeconds(10));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-name']").Change("Losing local draft"));
        var document = WorkflowCanvasDefinitionMapper.ToDefinition(WorkflowCanvasDefinitionMapper.FromDefinition(original, []));
        var competing = new WorkflowDocumentOwner(catalog, NullLogger.Instance, harness.Context.Services.GetRequiredService<WorkflowChanges>(), (_, _) => ValueTask.CompletedTask);
        var receipt = Assert.IsType<WorkflowDefinitionSaveOutcome.Accepted>(await competing.Operations.Save(
            new(original.Id, original.VersionId, "Winner", original.Description, original.Status, document.Graph, document.RuntimePolicy) {
                InputParameters = original.InputParameters, ExternalNamespace = original.ExternalNamespace, ExternalKey = original.ExternalKey
            }, CancellationToken.None));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        Assert.Null(cut.Instance.DocumentOwner.Accepted);
        Assert.Contains("The saved workflow changed", cut.Markup);
        Assert.Equal("Losing local draft", cut.Find("[data-testid='workflow-canvas-name']").GetAttribute("value"));
        Assert.Equal(receipt.Definition.VersionId, Assert.IsType<WorkflowDefinitionDetail>(await catalog.GetDefinitionAsync(original.Id)).Definition.VersionId);
    }

    [Fact]
    public async Task Native_save_receipt_survives_a_failed_parent_callback_without_a_second_version() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var catalog = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var original = await SaveAndReadAsync(catalog, CreateRequest());
        var callbacks = 0;
        var cut = harness.Context.Render<WorkflowCanvasEditor>(p => p.Add(x => x.Authorize, (_, _) => ValueTask.CompletedTask).Add(x => x.Definition, original)
            .Add(x => x.DefinitionSaved, _ => {
                callbacks++;
                throw new InvalidOperationException("private post-commit failure");
            }));
        cut.WaitForElement("[data-testid='workflow-canvas-save']", TimeSpan.FromSeconds(10));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        var receipt = Assert.IsType<WorkflowDefinition>(cut.Instance.DocumentOwner.Accepted);
        Assert.Equal(1, callbacks);
        Assert.Equal(receipt.VersionId, cut.FindComponent<WorkflowCanvasSurface>().Instance.AcceptedDefinition?.VersionId);
        Assert.Contains("Workflow saved. Catalog refresh is unavailable", cut.Markup);
        Assert.DoesNotContain("private post-commit failure", cut.Markup);
        Assert.Equal(receipt.VersionId, Assert.IsType<WorkflowDefinitionDetail>(await catalog.GetDefinitionAsync(original.Id)).Definition.VersionId);
    }

    [Fact]
    public async Task Native_title_edit_preserves_parameter_descriptors_and_stable_provenance() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var catalog = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var original = await SaveAndReadAsync(catalog, CreateRequest(nullableEndShapes: false));
        Assert.NotEmpty(original.InputParameters);
        var accepted = await EditTitleAsync(harness, original);
        await using var scope = harness.Context.Services.CreateAsyncScope();
        var freshOwner = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
        var actual = Assert.IsType<WorkflowDefinitionDetail>(await freshOwner.GetDefinitionAsync(accepted.Id, accepted.VersionId)).Definition;
        Assert.Equal(JsonSerializer.Serialize(original.InputParameters), JsonSerializer.Serialize(actual.InputParameters));
        Assert.Equal(original.ExternalNamespace, actual.ExternalNamespace);
        Assert.Equal(original.ExternalKey, actual.ExternalKey);
        Assert.Equal(original.TemplateKey, actual.TemplateKey);
        Assert.Equal(original.TemplatePackKey, actual.TemplatePackKey);
        Assert.Equal(original.TemplatePackVersion, actual.TemplatePackVersion);
        Assert.Equal(original.SourceHash, actual.SourceHash);
        Assert.Equal(ToPostgreSqlPrecision(original.CreatedAtUtc), actual.CreatedAtUtc);
        Assert.NotEqual(original.VersionId, actual.VersionId);
        Assert.Equal(original.Id, actual.Id);
        Assert.Equal("Edited title", actual.Name);
        var previous = Assert.IsType<WorkflowDefinitionDetail>(await freshOwner.GetDefinitionAsync(original.Id, original.VersionId));
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(previous.Definition));
    }

    [Fact]
    public async Task Native_title_edit_preserves_rich_shapes_explicit_and_null_ports_coordinates_and_hidden_settings() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var catalog = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var original = await SaveAndReadAsync(catalog, CreateRequest());
        var accepted = await EditTitleAsync(harness, original);
        await using var scope = harness.Context.Services.CreateAsyncScope();
        var actual = Assert.IsType<WorkflowDefinitionDetail>(await scope.ServiceProvider
            .GetRequiredService<IWorkflowCatalogService>().GetDefinitionAsync(accepted.Id, accepted.VersionId)).Definition;
        Assert.Equal(JsonSerializer.Serialize(original.Graph), JsonSerializer.Serialize(actual.Graph));
        Assert.Equal(JsonSerializer.Serialize(original with {
            Name = actual.Name, VersionId = actual.VersionId, UpdatedAtUtc = actual.UpdatedAtUtc,
            CreatedAtUtc = ToPostgreSqlPrecision(original.CreatedAtUtc)
        }), JsonSerializer.Serialize(actual));
    }

    [Fact]
    public async Task Sharing_a_legacy_workflow_keeps_its_import_identity_across_a_native_edit_and_withdrawal() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var catalog = services.GetRequiredService<IWorkflowCatalogService>();
        var sharing = services.GetRequiredService<RemoteWorkflowCatalog>();
        var source = Guid.NewGuid();
        var birth = DateTimeOffset.UnixEpoch.AddTicks(1234567);
        var request = CreateRequest(nullableEndShapes: false);
        var graph = request.Graph with {
            Nodes = request.Graph.Nodes.Select(node => node with {
                Settings = new(null, null, null, null, string.Empty, node.Settings.InputShape, node.Settings.ResultShape)
            }).ToArray()
        };
        var legacy = new WorkflowDefinition(WorkflowId.New(), WorkflowVersionId.New(), "Shared review", request.Description,
            WorkflowLifecycleStatus.Draft, graph, request.RuntimePolicy, birth, birth);
        var factory = services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>();
        DateTimeOffset storedBirth;
        await using (var database = await factory.CreateDbContextAsync()) {
            database.Set<WorkflowDefinitionRecord>().Add(WorkflowDefinitionRecord.FromDefinition(legacy, revision: 1));
            database.Set<WorkflowDefinitionHeadRecord>().Add(new() { WorkflowId = legacy.Id.Value, VersionId = legacy.VersionId.Value });
            await database.SaveChangesAsync();
            storedBirth = await database.Set<WorkflowDefinitionRecord>().Where(row => row.VersionId == legacy.VersionId.Value)
                .Select(row => row.CreatedAtUtc).SingleAsync();
        }

        var original = Assert.IsType<WorkflowDefinitionDetail>(await catalog.GetDefinitionAsync(legacy.Id)).Definition;
        Assert.Equal(birth, original.CreatedAtUtc);
        Assert.InRange(Math.Abs(storedBirth.UtcTicks - birth.UtcTicks), 1L, TimeSpan.TicksPerMicrosecond - 1);
        await ToggleAsync("Publish workflow");
        var first = Assert.IsType<RemoteWorkflowDocument>(await sharing.FindAsync(legacy.Id.Value, default));
        Assert.Equal(storedBirth, first.CreatedAtUtc);
        var localId = RemoteWorkflowDraftWriter.Prepare(source, first, [], legacy.RuntimePolicy).Definition.Id;

        var edited = await EditTitleAsync(harness, original);
        Assert.Equal(storedBirth, edited.CreatedAtUtc);
        var current = Assert.IsType<RemoteWorkflowDocument>(await sharing.FindAsync(legacy.Id.Value, default));
        Assert.Equal(edited.VersionId.Value, current.Row.VersionId);
        Assert.Equal(first.CreatedAtUtc, current.CreatedAtUtc);
        Assert.Equal(localId, RemoteWorkflowDraftWriter.Prepare(source, current, [], legacy.RuntimePolicy).Definition.Id);
        await ToggleAsync("Withdraw sharing");
        Assert.Null(await sharing.FindAsync(legacy.Id.Value, default));
        Assert.DoesNotContain(await sharing.ListAsync(default), row => row.Id == legacy.Id.Value);

        async Task ToggleAsync(string action) {
            var selected = Assert.Single(await catalog.ListDefinitionsAsync(), row => row.Id == legacy.Id);
            var control = harness.Context.Render<RemoteWorkflowSharingControl>(parameters => parameters
                .Add(component => component.Selected, selected)
                .Add(component => component.Authorize, (_, _) => ValueTask.CompletedTask)
                .Add(component => component.Write, (write, token) => write(token)));
            await control.InvokeAsync(() => control.Find("[data-testid=workflows-remote-sharing]").ClickAsync());
            var button = control.WaitForElement("[data-testid=workflows-remote-set-publication]:not([disabled])", TimeSpan.FromSeconds(10));
            Assert.Contains(action, button.TextContent, StringComparison.Ordinal);
            await control.InvokeAsync(() => button.ClickAsync());
            control.WaitForAssertion(() => Assert.Empty(control.FindAll("[data-testid=workflows-remote-publication-dialog]")));
        }
    }

    private static async Task<WorkflowDefinition> EditTitleAsync(ComponentTestHarness harness, WorkflowDefinition original) {
        WorkflowDefinition? accepted = null;
        var cut = harness.Context.Render<WorkflowCanvasEditor>(p => p.Add(x => x.Authorize, (_, _) => ValueTask.CompletedTask).Add(x => x.Definition, original)
            .Add(x => x.DefinitionSaved, value => accepted = value));
        cut.WaitForAssertion(() => Assert.Equal(original.Name,
            cut.Find("[data-testid='workflow-canvas-name']").GetAttribute("value")), TimeSpan.FromSeconds(10));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-name']").Change("Edited title"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        return Assert.IsType<WorkflowDefinition>(accepted);
    }

    private static async Task<WorkflowDefinition> SaveAndReadAsync(IWorkflowCatalogService catalog, WorkflowDefinitionSaveRequest request) {
        var accepted = await catalog.SaveDefinitionAsync(request);
        return Assert.IsType<WorkflowDefinitionDetail>(await catalog.GetDefinitionAsync(accepted.Id, accepted.VersionId)).Definition;
    }

    private static DateTimeOffset ToPostgreSqlPrecision(DateTimeOffset value)
        => new(value.Ticks - value.Ticks % 10, value.Offset);

    private static WorkflowDefinitionSaveRequest CreateRequest(bool nullableEndShapes = true) {
        var shape = new WorkflowValueShape(WorkflowValueShapeKind.Json,
            """{"type":"object","properties":{"caseSensitive":{"type":"string"}},"additionalProperties":true}""",
            "Rich fixture shape");
        var settings = new WorkflowNodeSettings(null, Guid.NewGuid(), WorkflowId.New(),
            WorkflowExternalRequestKind.Approval, "  Preserve exact instructions.\n", shape, shape) {
            ExecutorSettingsJson = """{ "future": {"value":1}, "Future": [2,3] }"""
        };
        var start = new WorkflowNode(new("start"), WorkflowNodeKind.Start, "Start",
            [new(new("explicit-output"), "Named output", WorkflowPortDirection.Output, shape, false)], settings, 0, -30);
        var end = new WorkflowNode(new("end"), WorkflowNodeKind.End, "End", [], settings with {
            InputShape = nullableEndShapes ? null : shape, ResultShape = nullableEndShapes ? null : shape
        }, -10, 0);
        var edge = new WorkflowEdge(new("original-edge"), start.Id, new("explicit-output"),
            end.Id, null, WorkflowEdgeKind.Direct, string.Empty);
        return new(null, null, "Native WF1 original", "Fixture description", WorkflowLifecycleStatus.Draft,
            new(start.Id, [start, end], [edge]), new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false)) {
            InputParameters = [
                new("message", "Message", WorkflowInputParameterKind.Text, true, "Preserve order and text", "$.message",
                    "initial", WorkflowInputParameterOptionSource.None, null, null, "Enter a message"),
                new("priority", "Priority", WorkflowInputParameterKind.Integer, false, "Bounded input", "$.priority", "2",
                    new(WorkflowInputParameterOptionSourceKind.Static, "message", [new("2", "Normal", "Exact option")]), 1, 5, "Priority")
            ],
            ExternalNamespace = "wf1.fixture",
            ExternalKey = Guid.NewGuid().ToString("N"),
            TemplateProvenance = new("wf1-rich", "wf1-fixtures", "1.0", new string('a', 64))
        };
    }
}
