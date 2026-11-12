using System.Data.Common;
using System.Text.Json;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Editing;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureNodeComparisonTests {
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Native_node_comparison_applies_only_locally_and_intervening_changes_prevent_an_obsolete_Apply(int intervening) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var projects = services.GetRequiredService<ProjectsService>();
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Native node comparison" })).Value;
        var originalSurface = await workbench.GetStructureAsync(project);
        var original = await workbench.CreateObjectAsync(project,
            new(ProjectObjectType.Note, "first", "", "first\nmiddle\nlast", $"project:{project:D}") {
                ExpectedProjectAdmission = originalSurface.ExpectedProjectAdmission
            });
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var canvas = page.FindComponent<CanvasWorkbench>().Instance;
        var opening = Guid.NewGuid();
        await page.InvokeAsync(() => canvas.OnComposerOpened(JsonSerializer.Serialize(new CanvasWorkbenchComposerOpening(opening, null,
            new(original.Id, original.Title, original.Notes)))));
        await using var peerScope = services.CreateAsyncScope();
        var peer = peerScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var peerNode = await peer.UpdateObjectAsync(project, original.Id,
            new("peer first", original.Subtitle, "peer first\nmiddle\nlast", original.StartUtc, original.EndUtc,
                original.MetadataJson, original.DurationSeconds, original.NodeReferences) {
                ExpectedProjectAdmission = originalSurface.ExpectedProjectAdmission,
                ExpectedNode = original
            });
        Assert.NotNull(peerNode);
        var local = new CanvasWorkbenchComposerDraft(opening, 2, false, true, null,
            new(original.Id, "first", "first\nmiddle\nlocal last"));

        harness.Context.JSInterop.Setup<CanvasWorkbenchComposerDraft?>("CanDoItAll.canvasWorkbench.readComposerDraft", _ => true).SetResult(local);
        await page.InvokeAsync(() => canvas.OnComposerComparisonRequested(JsonSerializer.Serialize(local)));

        var comparison = page.Find("[data-testid='structure-node-comparison']");
        Assert.Contains(original.Notes, comparison.TextContent, StringComparison.Ordinal);
        Assert.Contains(local.EditRequest!.Notes, comparison.TextContent, StringComparison.Ordinal);
        Assert.Contains(peerNode.Notes, comparison.TextContent, StringComparison.Ordinal);
        var stored = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == original.Id);
        Assert.Equal(peerNode.Notes, stored.Notes);
        Assert.Equal(peerNode.Title, stored.Title);
        CanvasWorkbenchComposerApplyRequest? applied = null;
        harness.Context.JSInterop.Setup<CanvasWorkbenchComposerApplyResult>("CanDoItAll.canvasWorkbench.applyComposerDraft", invocation => {
            applied = Assert.IsType<CanvasWorkbenchComposerApplyRequest>(invocation.Arguments[1]);
            return true;
        }).SetResult(new(CanvasWorkbenchComposerApplyStatus.Applied, 3));
        if (intervening == 1) {
            peerNode = await peer.UpdateObjectAsync(project, original.Id,
                new("Third writer", original.Subtitle, "Third writer", original.StartUtc, original.EndUtc,
                    original.MetadataJson, original.DurationSeconds, original.NodeReferences) {
                    ExpectedProjectAdmission = originalSurface.ExpectedProjectAdmission, ExpectedNode = peerNode
                });
        }

        if (intervening == 2) {
            var deleting = await peer.GetStructureAsync(project);
            Assert.Equal(1, await peer.DeleteObjectAsync(project, original.Id, mutationOwner: new("", "", "", "", "", "") {
                ExpectedProjectAdmission = deleting.ExpectedProjectAdmission,
                ExpectedGraph = ProjectStructureGraphStateGuard.Capture(deleting)
            }));
        }
        await page.Find("[data-testid='structure-node-compare-apply']").ClickAsync(new());
        if (intervening == 2) {
            Assert.Null(applied);
            Assert.DoesNotContain((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == original.Id);
            Assert.Contains("deleted", page.Find("[data-testid='structure-node-comparison-dialog']").TextContent, StringComparison.Ordinal);
            return;
        }

        stored = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == original.Id);
        Assert.Equal(peerNode!.Notes, stored.Notes);
        if (intervening == 1) {
            Assert.Null(applied);
            Assert.Contains("changed again", page.Find("[data-testid='structure-node-comparison-dialog']").TextContent, StringComparison.Ordinal);
            return;
        }
        Assert.NotNull(applied);
        Assert.Equal("peer first\nmiddle\nlocal last", applied.EditRequest!.Notes);
        Assert.Equal(peerNode.Title, applied.EditRequest.Title);
        Assert.True(applied.EditRequest.HasExplicitTitle);
        Assert.Empty(page.FindAll("[data-testid='structure-node-comparison-dialog']"));

        await page.InvokeAsync(() => canvas.OnNodeEdited(JsonSerializer.Serialize(applied.EditRequest with {
            ComposerOpeningId = opening, ComposerSubmissionId = Guid.NewGuid()
        })));

        stored = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == original.Id);
        Assert.Equal(applied.EditRequest.Notes, stored.Notes);
        Assert.Equal(peerNode.Title, stored.Title);
    }

    [Fact]
    public async Task Full_native_form_merges_keyed_numeric_fields_and_preserves_unknown_current_metadata() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Full typed node form" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var surface = await workbench.GetStructureAsync(project);
        var original = await workbench.CreateObjectAsync(project,
            new(ProjectObjectType.Infrastructure, "Server", "", "Original notes", $"project:{project:D}", ObjectSubtype: "remote-server",
                MetadataJson: """{"infrastructure":{"infrastructureKind":"remoteServer","host":"example.invalid","cpuCores":2,"memoryGb":4},"future":{"keep":true}}""") {
                ExpectedProjectAdmission = surface.ExpectedProjectAdmission
            });
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var canvas = page.FindComponent<CanvasWorkbench>().Instance;
        await page.InvokeAsync(() => canvas.OnContextAction(original.Id, "edit", 0, 0));
        var invocation = Assert.Single(harness.Context.JSInterop.Invocations["CanDoItAll.canvasWorkbench.openCreateComposer"]);
        var request = Assert.IsType<CanvasWorkbenchCreateActionRequest>(invocation.Arguments[2]);
        var opening = Guid.NewGuid();
        await page.InvokeAsync(() => canvas.OnComposerOpened(JsonSerializer.Serialize(new CanvasWorkbenchComposerOpening(opening, request, null))));
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(original.ObjectType, original.ObjectSubtype, out var definition));
        await using var peerScope = services.CreateAsyncScope();
        var peer = peerScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var peerUpdate = ProjectStructureNodeEditor.ComposeUpdate(definition, original, request with {
            InputValues = [new() { Key = "memoryGb", Value = "8" }]
        }) with { ExpectedProjectAdmission = surface.ExpectedProjectAdmission, ExpectedNode = original };
        var current = await peer.UpdateObjectAsync(project, original.Id, peerUpdate);
        Assert.NotNull(current);
        var local = new CanvasWorkbenchComposerDraft(opening, 5, false, true, request with {
            InputValues = request.InputValues!.Select(value => new CanvasWorkbenchInputValue {
                Key = value.Key, Value = value.Key == "cpuCores" ? "3.25" : value.Value
            }).ToArray()
        }, null);
        harness.Context.JSInterop.Setup<CanvasWorkbenchComposerDraft?>("CanDoItAll.canvasWorkbench.readComposerDraft", _ => true).SetResult(local);
        CanvasWorkbenchComposerApplyRequest? applied = null;
        harness.Context.JSInterop.Setup<CanvasWorkbenchComposerApplyResult>("CanDoItAll.canvasWorkbench.applyComposerDraft", args => {
            applied = Assert.IsType<CanvasWorkbenchComposerApplyRequest>(args.Arguments[1]);
            return true;
        }).SetResult(new(CanvasWorkbenchComposerApplyStatus.Applied, 6));

        await page.InvokeAsync(() => canvas.OnComposerComparisonRequested(JsonSerializer.Serialize(local)));
        await page.Find("[data-testid='structure-node-compare-apply']").ClickAsync(new());

        Assert.NotNull(applied?.CreateRequest);
        Assert.Equal("3.25", applied.CreateRequest.InputValues!.Single(value => value.Key == "cpuCores").Value);
        Assert.Equal("8", applied.CreateRequest.InputValues!.Single(value => value.Key == "memoryGb").Value);
        var beforeSave = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == original.Id);
        Assert.Equal(current.MetadataJson, beforeSave.MetadataJson);
        await page.InvokeAsync(() => canvas.OnCreateAction(JsonSerializer.Serialize(applied.CreateRequest with {
            ComposerOpeningId = opening, ComposerSubmissionId = Guid.NewGuid()
        })));
        var stored = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == original.Id);
        var metadata = ProjectObjectMetadataSerializer.Parse(stored.MetadataJson).Infrastructure!;
        Assert.Equal(3.25m, metadata.CpuCores);
        Assert.Equal(8, metadata.MemoryGb);
        using var json = JsonDocument.Parse(stored.MetadataJson);
        Assert.True(json.RootElement.GetProperty("future").GetProperty("keep").GetBoolean());
    }

    [Fact]
    public async Task Native_node_save_rolls_back_when_Structure_access_is_revoked_after_SQL() {
        var access = new EditingUiTestAccess();
        var probe = new RevokeAfterSql(access);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
        });
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Node authority" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var surface = await workbench.GetStructureAsync(project);
        var original = await workbench.CreateObjectAsync(project,
            new(ProjectObjectType.Note, "Original", "", "Original", $"project:{project:D}") {
                ExpectedProjectAdmission = surface.ExpectedProjectAdmission
            });
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var canvas = page.FindComponent<CanvasWorkbench>().Instance;
        var opening = Guid.NewGuid();
        await page.InvokeAsync(() => canvas.OnComposerOpened(JsonSerializer.Serialize(new CanvasWorkbenchComposerOpening(opening, null,
            new(original.Id, original.Title, original.Notes)))));
        probe.Armed = true;

        await page.InvokeAsync(() => canvas.OnNodeEdited(JsonSerializer.Serialize(new CanvasWorkbenchNodeEditRequest(original.Id, RevokeAfterSql.Target, RevokeAfterSql.Target) {
            ComposerOpeningId = opening, ComposerSubmissionId = Guid.NewGuid()
        })));

        Assert.True(probe.Reached);
        var stored = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == original.Id);
        Assert.Equal(original.Title, stored.Title);
        Assert.Equal(original.Notes, stored.Notes);
        Assert.Equal(ProjectStructureAuthoringResultKind.Rejected, Assert.Single(page.Instance.AuthoringOutcomes).Kind);
    }

    [Fact]
    public async Task Unmounting_Canvas_during_comparison_read_waits_for_handled_cancellation() {
        var hold = new HoldComparisonRead();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(hold), ServiceLifetime.Singleton));
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Retiring comparison owner" })).Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var surface = await workbench.GetStructureAsync(project);
        var original = await workbench.CreateObjectAsync(project,
            new(ProjectObjectType.Note, "Original title", "", "Original notes", $"project:{project:D}") {
                ExpectedProjectAdmission = surface.ExpectedProjectAdmission
            });
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var canvas = page.FindComponent<CanvasWorkbench>().Instance;
        var opening = Guid.NewGuid();
        await page.InvokeAsync(() => canvas.OnComposerOpened(JsonSerializer.Serialize(new CanvasWorkbenchComposerOpening(opening, null,
            new(original.Id, original.Title, original.Notes)))));
        var local = new CanvasWorkbenchComposerDraft(opening, 2, false, true, null,
            new(original.Id, "Local title", "Local notes"));
        hold.Armed = true;
        var pending = page.InvokeAsync(() => canvas.OnComposerComparisonRequested(JsonSerializer.Serialize(local)));
        Task disposal;
        try {
            await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            disposal = harness.Context.DisposeRenderedComponentsAsync();
            await hold.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.False(disposal.IsCompleted);
        } finally {
            hold.Release.TrySetResult();
        }
        await disposal.WaitAsync(TimeSpan.FromSeconds(20));
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.False(harness.Context.Renderer.UnhandledException.IsCompleted);
        var stored = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == original.Id);
        Assert.Equal(original.Title, stored.Title);
        Assert.Equal(original.Notes, stored.Notes);

        var reopened = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, project));
        reopened.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        Assert.False(harness.Context.Renderer.UnhandledException.IsCompleted);
    }

    private sealed class HoldComparisonRead : DbCommandInterceptor {
        public bool Armed { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (Armed && eventData.Context is WorkbenchDbContext) {
                Armed = false;
                using var registration = cancellationToken.Register(() => CancellationObserved.TrySetResult());
                Entered.TrySetResult();
                await Release.Task;
                cancellationToken.ThrowIfCancellationRequested();
            }
            return result;
        }
    }

    private sealed class RevokeAfterSql(EditingUiTestAccess access) : SaveChangesInterceptor {
        public const string Target = "Revoked node after SQL";
        public bool Armed { get; set; }
        public bool Reached { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Armed && result > 0 && eventData.Context is WorkbenchDbContext context &&
                context.ChangeTracker.Entries<ProjectObjectRecord>().Any(entry => entry.Entity.Title == Target)) {
                Assert.NotNull(context.Database.CurrentTransaction);
                Reached = true;
                Armed = false;
                access.Allowed = false;
            }
            return ValueTask.FromResult(result);
        }
    }
}
