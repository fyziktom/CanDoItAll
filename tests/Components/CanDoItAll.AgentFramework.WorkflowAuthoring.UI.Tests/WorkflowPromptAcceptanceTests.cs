using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkflowAuthoring;

public sealed class WorkflowPromptAcceptanceTests {
    [Fact]
    public async Task A_new_node_picker_from_a_retired_document_cannot_create_a_component() {
        await using var context = Context();
        WorkflowPromptPickerContext? picker = null;
        var calls = 0;
        var cut = Render(context, value => picker = value, (_, _) => {
            calls++;
            return Task.FromResult<WorkflowPromptBindingOutcome>(new WorkflowPromptBindingOutcome.Accepted(Component()));
        });
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-toggle-components']").ClickAsync());
        var retired = Assert.IsType<WorkflowPromptPickerContext>(picker);
        cut.Render(p => p.Add(x => x.Definition, Definition()));
        await cut.InvokeAsync(() => retired.Selected.InvokeAsync(Selection()));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Accepted_component_survives_callback_failure_and_a_stale_library_refresh() {
        await using var context = Context();
        WorkflowPromptPickerContext? picker = null;
        var component = Component();
        var calls = 0;
        WorkflowDefinitionSaveRequest? saved = null;
        var cut = Render(context, value => picker = value, (_, _) => {
            calls++;
            return Task.FromResult<WorkflowPromptBindingOutcome>(new WorkflowPromptBindingOutcome.Accepted(component));
        });
        cut.Render(p => p.Add(x => x.ComponentLibraryChanged, () => throw new InvalidOperationException("private refresh failure"))
            .Add(x => x.DocumentOperations, new((request, _) => {
                saved = request;
                return Task.FromResult<WorkflowDefinitionSaveOutcome>(new WorkflowDefinitionSaveOutcome.Rejected(WorkflowSaveRejection.Validation));
            }, (_, _) => Task.FromResult(WorkflowValidationResult.Success))));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-toggle-components']").ClickAsync());
        await cut.InvokeAsync(() => picker!.Selected.InvokeAsync(Selection()));
        cut.Render(p => p.Add(x => x.Components, []));
        Assert.Contains("The component is saved and retained", cut.Markup);
        Assert.Contains(component.Name, cut.Markup);
        Assert.DoesNotContain("private refresh failure", cut.Markup);
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        Assert.Equal(component.Id, Assert.Single(saved!.Graph.Nodes, node => node.Kind == WorkflowNodeKind.LlmCall).Settings.ComponentId);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Late_accepted_component_does_not_add_a_node_to_a_replacement_document() {
        await using var context = Context();
        WorkflowPromptPickerContext? picker = null;
        var held = new TaskCompletionSource<WorkflowPromptBindingOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbacks = 0;
        var component = Component();
        var cut = Render(context, value => picker = value, (_, _) => held.Task);
        cut.Render(p => p.Add(x => x.ComponentLibraryChanged, () => callbacks++));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-toggle-components']").ClickAsync());
        var pending = cut.InvokeAsync(() => picker!.Selected.InvokeAsync(Selection()));
        var replacement = Definition();
        cut.Render(p => p.Add(x => x.Definition, replacement));
        held.SetResult(new WorkflowPromptBindingOutcome.Accepted(component));
        await pending;
        cut.Render(p => p.Add(x => x.Components, []));
        Assert.Equal(0, callbacks);
        Assert.Equal("2", cut.FindAll(".cw-stat-chip").Single(item => item.TextContent.Contains("Nodes")).QuerySelector("strong")!.TextContent);
        Assert.Contains(component.Name, cut.Markup);
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    private static IRenderedComponent<WorkflowCanvasSurface> Render(BunitContext context, Action<WorkflowPromptPickerContext> capture,
        Func<WorkflowPromptBindingRequest, CancellationToken, Task<WorkflowPromptBindingOutcome>> bind)
        => context.Render<WorkflowCanvasSurface>(p => p.Add(x => x.Definition, Definition())
            .Add(x => x.ProviderOptions, [Provider]).Add(x => x.BindPrompt, bind)
            .Add(x => x.PromptPicker, picker => builder => capture(picker))
            .Add(x => x.DocumentOperations, new((_, _) => throw new InvalidOperationException(),
                (_, _) => Task.FromResult(WorkflowValidationResult.Success)))
            .Add(x => x.PreviewOperations, new(_ => WorkflowPreviewRequirements.Empty,
                _ => Task.FromResult<IReadOnlyList<WorkflowPreviewProject>>([]), (_, _, _) => throw new InvalidOperationException())));

    private static readonly WorkflowProviderOption Provider = new(Guid.NewGuid(), "Source", ProviderKind.OpenAi,
        ProviderTransportKind.Responses, ProviderProfilePurpose.Chat, "opaque.Route", ["opaque.Route"], true, true, true, true, true, false) {
        IsSourceManaged = true, ModelCatalog = [new("opaque.Route", "Native source model")]
    };
    private static WorkflowDefinition Definition() => WorkflowCanvasDefinitionMapper.ToDefinition(WorkflowCanvasDefinitionMapper.CreateDraft([]));
    private static PromptGallerySelection Selection() => new(Guid.NewGuid(), Guid.NewGuid(), 1, "Accepted prompt", "Fixture",
        PromptGalleryItemKind.FullPrompt, "Pinned instructions", [], [], new());
    private static LlmCallComponent Component() => new(WorkflowComponentId.New(), "Accepted component", Provider.ProviderProfileId,
        Provider.DefaultModel, WorkflowModality.Text, new(.2, 800, false, string.Empty), "Pinned instructions", WorkflowValueShape.Text,
        WorkflowValueShape.Text, AgentPermissionsPolicy.Default, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
