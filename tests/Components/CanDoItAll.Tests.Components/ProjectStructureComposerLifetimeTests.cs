using System.Text.Json;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureComposerLifetimeTests {
    [Fact]
    public async Task Runtime_validation_rejects_without_a_write_and_a_fresh_corrected_opening_can_commit() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Runtime composer validation" })).Value;
        var page = harness.Context.Render<ProjectStructurePage>(p => p.Add(c => c.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var canvas = page.FindComponent<CanvasWorkbench>();
        var root = Assert.Single(canvas.Instance.Surface.Nodes, node => node.Id == $"project:{project:D}");
        var openingId = Guid.NewGuid();
        var request = new CanvasWorkbenchCreateActionRequest(
            "add-script-console", root.Id, 0, 0, root.Id, "Runtime script", "", "Retained draft",
            "child", "dialog", "console", null,
            [new() { Key = "scriptKind", Value = "console" }, new() { Key = "command", Value = "" }]) {
            ComposerOpeningId = openingId, ComposerSubmissionId = Guid.NewGuid()
        };
        await page.InvokeAsync(() => canvas.Instance.OnComposerOpened(JsonSerializer.Serialize(new CanvasWorkbenchComposerOpening(openingId, request, null))));
        await page.InvokeAsync(() => canvas.Instance.OnCreateAction(JsonSerializer.Serialize(request)));

        var outcome = Assert.Single(page.Instance.AuthoringOutcomes);
        Assert.Equal(ProjectStructureAuthoringResultKind.Rejected, outcome.Kind);
        Assert.Equal(AgentToolEffectState.NotCommitted, Assert.IsType<ProjectStructureAgentException>(outcome.Failure).EffectState);
        Assert.Contains("typed command or explicit script path", outcome.Message, StringComparison.Ordinal);
        var acknowledgement = Assert.IsType<CanvasWorkbenchComposerResult>(Assert.Single(
            harness.Context.JSInterop.Invocations["CanDoItAll.canvasWorkbench.acknowledgeComposer"]).Arguments[1]);
        Assert.Equal(CanvasWorkbenchComposerResultKind.Rejected, acknowledgement.Kind);
        Assert.DoesNotContain((await workbench.GetStructureAsync(project)).Nodes, node => node.ObjectType == ProjectObjectType.Script);

        var corrected = request with {
            ComposerOpeningId = null, ComposerSubmissionId = Guid.NewGuid(),
            InputValues = [new() { Key = "scriptKind", Value = "console" },
                new() { Key = "command", Value = "dotnet --version" },
                new() { Key = "workingDirectory", Value = harness.ActiveProfile.WorkspaceRootPath }]
        };
        await page.InvokeAsync(() => canvas.Instance.OnCreateAction(JsonSerializer.Serialize(corrected with { ComposerOpeningId = openingId })));
        Assert.Single(page.Instance.AuthoringOutcomes);
        Assert.DoesNotContain((await workbench.GetStructureAsync(project)).Nodes, node => node.ObjectType == ProjectObjectType.Script);

        await page.InvokeAsync(() => CanvasComposerTestDispatch.CreateAsync(canvas.Instance, JsonSerializer.Serialize(corrected)));
        var stored = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.ObjectType == ProjectObjectType.Script);
        Assert.Equal(request.Notes, stored.Notes);
        Assert.Equal("dotnet --version", ProjectObjectMetadataSerializer.Parse(stored.MetadataJson).Script?.Command);
        Assert.Contains(page.Instance.AuthoringOutcomes, item => item.Kind == ProjectStructureAuthoringResultKind.Committed && item.Node?.Id == stored.Id);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Accepted_edit_keeps_original_outcome_after_close_or_replacement(bool loseReply, bool replace) {
        var gate = new ProjectStructureHierarchyLifetimeTests.OwnerWriteGate { LoseCommitReply = loseReply };
        await using var harness = await ProjectStructureHierarchyLifetimeTests.CreateHarnessAsync(gate);
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Delayed composer owner" })).Value;
        var note = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Original", "", "Notes", $"project:{project:D}"));
        var neighbor = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Neighbor", "", "Untouched", $"project:{project:D}"));
        var page = harness.Context.Render<ProjectStructurePage>(p => p.Add(c => c.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var canvas = page.FindComponent<CanvasWorkbench>();
        var id = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var request = new CanvasWorkbenchNodeEditRequest(note.Id, "Accepted", "Accepted original body") {
            ComposerOpeningId = id, ComposerSubmissionId = submissionId
        };
        await page.InvokeAsync(() => canvas.Instance.OnComposerOpened(JsonSerializer.Serialize(new CanvasWorkbenchComposerOpening(id, null, request))));
        gate.ArmNode(project, note.Id);
        var pending = page.InvokeAsync(() => canvas.Instance.OnNodeEdited(JsonSerializer.Serialize(request)));
        Guid? replacementId = null;
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await page.InvokeAsync(() => canvas.Instance.OnNodeEdited(JsonSerializer.Serialize(request)));
            await page.InvokeAsync(() => canvas.Instance.OnComposerClosed(id));
            if (replace) {
                replacementId = Guid.NewGuid();
                await page.InvokeAsync(() => canvas.Instance.OnComposerOpened(JsonSerializer.Serialize(new CanvasWorkbenchComposerOpening(replacementId.Value, null,
                    new(neighbor.Id, neighbor.Title, "Replacement draft")))));
            }
        } finally {
            gate.Release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        var outcome = Assert.Single(page.Instance.AuthoringOutcomes);
        Assert.Equal(loseReply ? ProjectStructureAuthoringResultKind.Unconfirmed : ProjectStructureAuthoringResultKind.Committed, outcome.Kind);
        Assert.Equal(id, outcome.OpeningId);
        Assert.Equal(note.Id, outcome.SourceNodeId);
        var acknowledgement = Assert.IsType<CanvasWorkbenchComposerResult>(Assert.Single(
            harness.Context.JSInterop.Invocations["CanDoItAll.canvasWorkbench.acknowledgeComposer"]).Arguments[1]);
        Assert.Equal(id, acknowledgement.OpeningId);
        Assert.Equal(submissionId, acknowledgement.SubmissionId);
        Assert.Equal(loseReply ? CanvasWorkbenchComposerResultKind.Unconfirmed : CanvasWorkbenchComposerResultKind.Committed,
            acknowledgement.Kind);
        var stored = await workbench.GetStructureAsync(project);
        Assert.Equal(request.Notes, Assert.Single(stored.Nodes, node => node.Id == note.Id).Notes);
        Assert.Equal(neighbor.Notes, Assert.Single(stored.Nodes, node => node.Id == neighbor.Id).Notes);
        await page.InvokeAsync(() => canvas.Instance.OnNodeEdited(JsonSerializer.Serialize(request)));
        Assert.Single(page.Instance.AuthoringOutcomes);
        if (replacementId is { } next) {
            await page.InvokeAsync(() => canvas.Instance.OnNodeEdited(JsonSerializer.Serialize(new CanvasWorkbenchNodeEditRequest(neighbor.Id, "Replacement", "Replacement draft") { ComposerOpeningId = next })));
            Assert.Equal("Replacement draft", Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == neighbor.Id).Notes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closed_or_recreated_original_opening_cannot_write(bool recreate) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Retired composer" })).Value;
        var note = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Original", "", "Retained", $"project:{project:D}"));
        var page = harness.Context.Render<ProjectStructurePage>(p => p.Add(c => c.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var canvas = page.FindComponent<CanvasWorkbench>();
        var id = Guid.NewGuid();
        var edit = new CanvasWorkbenchNodeEditRequest(note.Id, "Stale", "Should not write") { ComposerOpeningId = id };
        await page.InvokeAsync(() => canvas.Instance.OnComposerOpened(JsonSerializer.Serialize(new CanvasWorkbenchComposerOpening(id, null, edit))));
        if (recreate) {
            var native = harness.Context.Services.GetRequiredService<ProjectNativeLifecycleOperations>();
            var context = await native.GetContextAsync();
            var original = native.Freeze(new ProjectLifecycleCommand.Delete((await projects.GetAsync(project)).ExpectedDefinition!),
                ProjectNativeLifecycleOperations.Scope(context), context, Guid.NewGuid());
            Assert.IsType<ProjectLifecycleOutcome.Accepted>(await native.DispatchAsync(original.Submission));
            Assert.True((await projects.CreateAsync(project, new() { Name = "Replacement lifetime" })).IsSuccess);
        } else {
            await page.InvokeAsync(() => canvas.Instance.OnComposerClosed(id));
        }
        await page.InvokeAsync(() => canvas.Instance.OnNodeEdited(JsonSerializer.Serialize(edit)));
        Assert.DoesNotContain((await workbench.GetStructureAsync(project)).Nodes, node => node.Notes == edit.Notes);
        if (recreate) {
            Assert.Equal(ProjectStructureAuthoringResultKind.Rejected, Assert.Single(page.Instance.AuthoringOutcomes).Kind);
        } else {
            Assert.Empty(page.Instance.AuthoringOutcomes);
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 100)]
    [InlineData(-12, 0)]
    [InlineData(1.125, 0)]
    public void Explicit_canvas_coordinates_include_zero_and_negative_values(double x, double y) {
        var node = Node();
        var plan = new CanDoItAll.Modules.Workbench.CanvasAdapters.ProjectStructurePlacementPolicy().ResolveCreatePlacementPlan([node], node, node,
            Request(node, []) with { PlacementKind = "canvas", X = x, Y = y });
        Assert.Equal(x, plan.Placement.X);
        Assert.Equal(y, plan.Placement.Y);
    }

    [Fact]
    public async Task Original_generic_edit_refuses_a_native_target_whose_type_changed() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        harness.Context.JSInterop.Setup<bool>("CanDoItAll.canvasWorkbench.create", _ => true).SetResult(true);
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var project = (await projects.SaveAsync(new() { Name = "Composer original owner" })).Value;
        var note = await workbench.CreateObjectAsync(project, new(ProjectObjectType.Note, "Original note", "", "Original notes", $"project:{project:D}"));
        var page = harness.Context.Render<ProjectStructurePage>(p => p.Add(c => c.ProjectId, project));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var canvas = page.FindComponent<CanvasWorkbench>();
        await page.InvokeAsync(() => canvas.Instance.OnContextAction(note.Id, "edit", 0, 0));
        var invocation = Assert.Single(harness.Context.JSInterop.Invocations["CanDoItAll.canvasWorkbench.openCreateComposer"]);
        var original = Assert.IsType<CanvasWorkbenchCreateActionRequest>(invocation.Arguments[2]);
        var openingId = Guid.NewGuid();
        await page.InvokeAsync(() => canvas.Instance.OnComposerOpened(JsonSerializer.Serialize(new CanvasWorkbenchComposerOpening(openingId, original, null))));
        await workbench.ReclassifyObjectAsync(project, note.Id,
            new(ProjectObjectType.ProjectBlock, "decision", "External change", "", "Retained notes") {
                ExpectedNode = note, ExpectedProjectAdmission = (await workbench.GetStructureAsync(project)).ExpectedProjectAdmission
            });
        await page.InvokeAsync(() => canvas.Instance.OnCreateAction(JsonSerializer.Serialize(original with { Title = "Stale edit", ComposerOpeningId = openingId })));
        var stored = Assert.Single((await workbench.GetStructureAsync(project)).Nodes, node => node.Id == note.Id);
        Assert.Equal("External change", stored.Title);
        Assert.Equal("Retained notes", stored.Notes);
    }

    [Fact]
    public void Generic_round_trip_preserves_fine_values_and_unknown_metadata() {
        var node = Node();
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(node.ObjectType, node.ObjectSubtype, out var definition));
        var values = ProjectStructureNodeEditor.BuildInputValues(definition, node);
        var update = ProjectStructureNodeEditor.ComposeUpdate(definition, node, Request(node, values));
        using var json = JsonDocument.Parse(update.MetadataJson);
        Assert.Equal(12.34567m, json.RootElement.GetProperty("infrastructure").GetProperty("cpuCores").GetDecimal());
        Assert.Equal("retained", json.RootElement.GetProperty("extension").GetProperty("value").GetString());
        Assert.Equal("nested", json.RootElement.GetProperty("infrastructure").GetProperty("custom").GetString());
        Assert.Equal(node.StartUtc, update.StartUtc);
        Assert.Equal(node.DurationSeconds, update.DurationSeconds);
        var echoed = ProjectStructureNodeEditor.ComposeUpdate(definition, node, Request(node,
            [new() { Key = "startUtc", Value = node.StartUtc!.Value.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture) }]));
        Assert.Equal(node.StartUtc, echoed.StartUtc);
    }

    [Theory]
    [InlineData("cpuCores", "-")]
    [InlineData("startUtc", "not-a-date")]
    [InlineData("infrastructureKind", "unknown-kind")]
    public void Invalid_generic_values_are_rejected_instead_of_clearing_or_retaining_hidden_values(string key, string value) {
        var node = Node();
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(node.ObjectType, node.ObjectSubtype, out var definition));
        Assert.Throws<InvalidOperationException>(() => ProjectStructureNodeEditor.ComposeUpdate(definition, node,
            Request(node, [new() { Key = key, Value = value }])));
    }

    private static CanvasWorkbenchCreateActionRequest Request(ProjectStructureNode node, IReadOnlyList<CanvasWorkbenchInputValue> values)
        => new("edit:fixture", node.Id, 0, 0, node.ParentId, "Edited title", node.Subtitle, node.Notes, "edit", "dialog", node.ObjectSubtype, null, values);

    private static ProjectStructureNode Node() => new("node", "parent", ProjectObjectType.Infrastructure, "remote-server", "Server", "", "Ready", "Notes", "", "", null,
        "", "", "", 0, 0, new("rect", "#0369a1", "S", "Server"), [], "progress", 0, "", "", "", [], 0,
        StartUtc: DateTimeOffset.Parse("2026-10-05T12:34:56.1234567Z", System.Globalization.CultureInfo.InvariantCulture),
        MetadataJson: """{"infrastructure":{"infrastructureKind":"remoteServer","cpuCores":12.34567,"custom":"nested"},"extension":{"value":"retained"}}""",
        DurationSeconds: 123);
}
