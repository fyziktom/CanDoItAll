using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkflowAuthoring;

public sealed class WorkflowCanvasAcceptanceTests {
    [Fact]
    public async Task Failed_validation_is_reported_without_certifying_or_discarding_the_draft() {
        await using var context = CreateContext();
        var cut = Render(context, Definition(), new((_, _) => throw new InvalidOperationException(),
            (_, _) => throw new InvalidOperationException("private validation failure")));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-name']").Change("Retained draft"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-validate']").ClickAsync());
        Assert.Equal("Retained draft", cut.Find("[data-testid='workflow-canvas-name']").GetAttribute("value"));
        Assert.DoesNotContain("private validation failure", cut.Markup);
        Assert.Equal("Not validated", cut.FindAll(".cw-stat-chip").Single(item => item.TextContent.Contains("Validation")).QuerySelector("strong")!.TextContent);
        Assert.Contains("Validation is unavailable", cut.Markup);
    }

    [Fact]
    public async Task Failed_preview_preparation_does_not_dispatch_or_break_the_editor() {
        await using var context = CreateContext();
        var calls = 0;
        var preview = new WorkflowPreviewOperations(_ => throw new InvalidOperationException("private configuration failure"),
            _ => Task.FromResult<IReadOnlyList<WorkflowPreviewProject>>([]), (_, _, _) => {
                calls++;
                return Task.FromResult<WorkflowPreviewOutcome>(new WorkflowPreviewOutcome.Rejected("Unused"));
            });
        var cut = Render(context, Definition(), new((_, _) => throw new InvalidOperationException(), Valid), preview: preview);
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-run-preview']").ClickAsync());
        Assert.Equal(0, calls);
        Assert.DoesNotContain("private configuration failure", cut.Markup);
        Assert.Contains("Preview preparation is unavailable", cut.Markup);
    }

    [Fact]
    public async Task Held_save_adopts_accepted_version_and_keeps_later_edits_for_the_next_save() {
        await using var context = CreateContext();
        var original = Definition();
        var held = new TaskCompletionSource<WorkflowDefinitionSaveOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = new List<WorkflowDefinitionSaveRequest>();
        var cut = Render(context, original, new(async (request, _) => {
            requests.Add(request);
            return requests.Count == 1 ? await held.Task : new WorkflowDefinitionSaveOutcome.Accepted(Accept(original, request));
        }, Valid));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-name']").Change("Submitted"));
        var save = cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        cut.WaitForAssertion(() => Assert.Single(requests));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-name']").Change("Later edit"));
        var accepted = Accept(original, requests[0]);
        held.SetResult(new WorkflowDefinitionSaveOutcome.Accepted(accepted));
        await save;
        Assert.Equal(accepted.VersionId, cut.Instance.AcceptedDefinition?.VersionId);
        Assert.Equal("Later edit", cut.Find("[data-testid='workflow-canvas-name']").GetAttribute("value"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        Assert.Equal(2, requests.Count);
        Assert.Equal(accepted.VersionId, requests[1].ExpectedVersionId);
        Assert.Equal("Later edit", requests[1].Name);
    }

    [Fact]
    public async Task Accepted_save_survives_a_failed_parent_refresh() {
        await using var context = CreateContext();
        var original = Definition();
        var requests = new List<WorkflowDefinitionSaveRequest>();
        var cut = Render(context, original, new((request, _) => {
            requests.Add(request);
            return Task.FromResult<WorkflowDefinitionSaveOutcome>(new WorkflowDefinitionSaveOutcome.Accepted(Accept(original, request)));
        }, Valid), () => throw new InvalidOperationException("Private refresh failure"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        var accepted = Assert.IsType<WorkflowDefinition>(cut.Instance.AcceptedDefinition);
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        Assert.Equal(2, requests.Count);
        Assert.Equal(accepted.Id, requests[1].Id);
        Assert.Equal(accepted.VersionId, requests[1].ExpectedVersionId);
        Assert.DoesNotContain("Private refresh failure", cut.Markup);
    }

    [Fact]
    public async Task Unknown_save_blocks_replay_while_a_conflict_keeps_its_original_expected_version() {
        await using var context = CreateContext();
        var original = Definition();
        var requests = new List<WorkflowDefinitionSaveRequest>();
        var cut = Render(context, original, new((request, _) => {
            requests.Add(request);
            return Task.FromResult<WorkflowDefinitionSaveOutcome>(requests.Count == 1
                ? new WorkflowDefinitionSaveOutcome.Rejected(WorkflowSaveRejection.Conflict)
                : new WorkflowDefinitionSaveOutcome.Unknown());
        }, Valid));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        Assert.Equal(original.VersionId, requests[1].ExpectedVersionId);
        Assert.True(cut.Find("[data-testid='workflow-canvas-save']").HasAttribute("disabled"));
        Assert.Null(cut.Instance.AcceptedDefinition);
    }

    [Fact]
    public async Task Validation_does_not_certify_an_A_to_B_to_A_edit_sequence() {
        await using var context = CreateContext();
        var original = Definition();
        var held = new TaskCompletionSource<WorkflowValidationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = Render(context, original, new((_, _) => throw new InvalidOperationException(), (_, _) => held.Task));
        var validation = cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-validate']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-name']").Change("B"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-name']").Change(original.Name));
        held.SetResult(WorkflowValidationResult.Success);
        await validation;
        Assert.DoesNotContain(context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary == "Workflow canvas valid");
    }

    [Fact]
    public async Task Independent_editors_of_the_same_workflow_have_distinct_canvas_instances() {
        await using var context = CreateContext();
        var definition = Definition();
        var operations = new WorkflowDocumentOperations((_, _) => throw new InvalidOperationException(), Valid);
        var first = Render(context, definition, operations);
        var second = Render(context, definition, operations);
        Assert.NotEqual(first.FindComponent<CanvasWorkbench>().Instance.Surface.SurfaceId,
            second.FindComponent<CanvasWorkbench>().Instance.Surface.SurfaceId);
        await first.InvokeAsync(() => first.Find("[data-testid='workflow-canvas-name']").Change("Only first"));
        Assert.Equal(definition.Name, second.Find("[data-testid='workflow-canvas-name']").GetAttribute("value"));
    }

    [Fact]
    public async Task Accepted_save_does_not_replace_a_later_A_to_B_to_A_edit_with_normalization() {
        await using var context = CreateContext();
        var original = Definition() with { Name = " A " };
        var held = new TaskCompletionSource<WorkflowDefinitionSaveOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = Render(context, original, new((_, _) => held.Task, Valid));
        var save = cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-name']").Change("B"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-name']").Change(" A "));
        var accepted = original with { Name = "A", VersionId = WorkflowVersionId.New() };
        held.SetResult(new WorkflowDefinitionSaveOutcome.Accepted(accepted));
        await save;
        Assert.Equal(" A ", cut.Find("[data-testid='workflow-canvas-name']").GetAttribute("value"));
        Assert.Equal(accepted.VersionId, cut.Instance.AcceptedDefinition?.VersionId);
    }

    [Fact]
    public async Task Preview_submits_the_dialog_snapshot_and_does_not_certify_later_edits() {
        await using var context = CreateContext();
        var original = Definition();
        WorkflowPreviewSubmission? submitted = null;
        var preview = new WorkflowPreviewOperations(_ => new([], [
            new(new("start"), "Start", WorkflowExecutorIds.ImageGeneration, "Fixture simulation", "{}")
        ]), _ => Task.FromResult<IReadOnlyList<WorkflowPreviewProject>>([]), (request, _, _) => {
            submitted = request;
            return Task.FromResult<WorkflowPreviewOutcome>(new WorkflowPreviewOutcome.Completed(
                WorkflowValidationResult.Success, null, true, true, "Fixture complete"));
        });
        var cut = Render(context, original, new((_, _) => throw new InvalidOperationException(), Valid), preview: preview);
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-run-preview']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-name']").Change("Later draft"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-preview-input-json']").Change("{\"input\":42}"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-preview-input-run']").ClickAsync());
        Assert.Equal(original.Name, Assert.IsType<WorkflowPreviewSubmission>(submitted).Definition.Name);
        Assert.Equal("{\"input\":42}", submitted.InputJson);
        Assert.Equal("Not validated", cut.FindAll(".cw-stat-chip").Single(item => item.TextContent.Contains("Validation")).QuerySelector("strong")!.TextContent);
    }

    [Fact]
    public async Task Late_project_read_cannot_fill_a_reopened_preview_dialog() {
        await using var context = CreateContext();
        var first = new TaskCompletionSource<IReadOnlyList<WorkflowPreviewProject>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        var preview = new WorkflowPreviewOperations(_ => new([], [
            new(new("start"), "Start", WorkflowExecutorIds.ImageGeneration, "Fixture simulation", "{}")
        ]), _ => ++count == 1 ? first.Task : Task.FromResult<IReadOnlyList<WorkflowPreviewProject>>([]),
            (_, _, _) => throw new InvalidOperationException());
        var cut = Render(context, Definition(), new((_, _) => throw new InvalidOperationException(), Valid), preview: preview);
        var opening = cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-run-preview']").ClickAsync());
        cut.WaitForElement("[data-testid='workflow-canvas-preview-input-dialog']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-preview-input-cancel']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-run-preview']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-preview-project-id']").Change("later raw value"));
        first.SetResult([new(Guid.NewGuid(), "Retired project")]);
        await opening;
        Assert.DoesNotContain("Retired project", cut.Markup);
        Assert.Equal("later raw value", cut.Find("[data-testid='workflow-canvas-preview-project-id']").GetAttribute("value"));
    }

    [Fact]
    public async Task Unknown_preview_retains_reserved_identity_and_blocks_another_dispatch() {
        await using var context = CreateContext();
        var reserved = WorkflowRunId.New();
        var calls = 0;
        var preview = new WorkflowPreviewOperations(_ => WorkflowPreviewRequirements.Empty,
            _ => Task.FromResult<IReadOnlyList<WorkflowPreviewProject>>([]), (_, _, _) => {
                calls++;
                return Task.FromResult<WorkflowPreviewOutcome>(new WorkflowPreviewOutcome.Unknown(reserved));
            });
        var cut = Render(context, Definition(), new((_, _) => throw new InvalidOperationException(), Valid), preview: preview);
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-run-preview']").ClickAsync());
        Assert.Equal(reserved, cut.Instance.AcceptedPreviewRunId);
        Assert.True(cut.Find("[data-testid='workflow-canvas-run-preview']").HasAttribute("disabled"));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(WorkflowNodeKind.AgentStep, "workflow-canvas-node-agent-id")]
    [InlineData(WorkflowNodeKind.Subworkflow, "workflow-canvas-node-subworkflow-id")]
    public async Task Invalid_native_identity_input_is_retained_and_blocks_save(WorkflowNodeKind kind, string testId) {
        await using var context = CreateContext();
        var draft = WorkflowCanvasDefinitionMapper.CreateDraft([]);
        var node = WorkflowCanvasDefinitionMapper.CreateNode(kind, draft.Nodes, [], 0, 0);
        draft.Nodes.Insert(1, node);
        var calls = 0;
        var cut = Render(context, WorkflowCanvasDefinitionMapper.ToDefinition(draft), new((_, _) => {
            calls++;
            return Task.FromResult<WorkflowDefinitionSaveOutcome>(new WorkflowDefinitionSaveOutcome.Unknown());
        }, Valid));
        await cut.InvokeAsync(() => cut.FindAll("[data-testid='workflow-canvas-select-node']")[1].ClickAsync());
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-tab-node']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find($"[data-testid='{testId}']").Change("invalid original identity"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        Assert.Equal(0, calls);
        Assert.Equal("invalid original identity", cut.Find($"[data-testid='{testId}']").GetAttribute("value"));
    }

    [Fact]
    public async Task Invalid_policy_input_is_retained_across_inspectors_and_blocks_save() {
        await using var context = CreateContext();
        var draft = WorkflowCanvasDefinitionMapper.CreateDraft([]);
        var node = WorkflowCanvasDefinitionMapper.CreateNode(WorkflowNodeKind.Executor, draft.Nodes, [], 0, 0);
        draft.Nodes.Insert(1, node);
        var calls = 0;
        var cut = Render(context, WorkflowCanvasDefinitionMapper.ToDefinition(draft), new((_, _) => {
            calls++;
            return Task.FromResult<WorkflowDefinitionSaveOutcome>(new WorkflowDefinitionSaveOutcome.Unknown());
        }, Valid));
        await cut.InvokeAsync(() => cut.FindAll("[data-testid='workflow-canvas-select-node']")[1].ClickAsync());
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-tab-node']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-executor-timeout']").Change("99999999999999999999"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-open-selected-node-details']").ClickAsync());
        Assert.Equal("99999999999999999999", cut.Find("[data-testid='workflow-canvas-node-modal-timeout']").GetAttribute("value"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Parent_metadata_rerender_after_new_save_retains_the_accepted_identity_and_draft() {
        await using var context = CreateContext();
        var original = Definition();
        var requests = new List<WorkflowDefinitionSaveRequest>();
        var cut = Render(context, null, new((request, _) => {
            requests.Add(request);
            return Task.FromResult<WorkflowDefinitionSaveOutcome>(new WorkflowDefinitionSaveOutcome.Accepted(Accept(original, request)));
        }, Valid), () => throw new InvalidOperationException("Fixture refresh failure"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-name']").Change("Kept new draft"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        var accepted = Assert.IsType<WorkflowDefinition>(cut.Instance.AcceptedDefinition);
        cut.Render(p => p.Add(x => x.Components, []));
        Assert.Equal("Kept new draft", cut.Find("[data-testid='workflow-canvas-name']").GetAttribute("value"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        Assert.Equal(accepted.Id, requests[1].Id);
        Assert.Equal(accepted.VersionId, requests[1].ExpectedVersionId);
    }

    [Fact]
    public async Task A_replacement_document_is_not_blocked_or_overwritten_by_a_retired_save() {
        await using var context = CreateContext();
        var first = Definition();
        var next = Definition() with { Name = "Replacement" };
        var held = new TaskCompletionSource<WorkflowDefinitionSaveOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = Render(context, first, new((_, _) => held.Task, Valid));
        var save = cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        cut.Render(p => p.Add(x => x.Definition, next));
        Assert.False(cut.Find("[data-testid='workflow-canvas-save']").HasAttribute("disabled"));
        held.SetResult(new WorkflowDefinitionSaveOutcome.Accepted(first with { VersionId = WorkflowVersionId.New() }));
        await save;
        Assert.Equal("Replacement", cut.Find("[data-testid='workflow-canvas-name']").GetAttribute("value"));
    }

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    private static IRenderedComponent<WorkflowCanvasSurface> Render(BunitContext context, WorkflowDefinition? definition,
        WorkflowDocumentOperations operations, Action? accepted = null, WorkflowPreviewOperations? preview = null)
        => context.Render<WorkflowCanvasSurface>(p => p.Add(x => x.Definition, definition)
            .Add(x => x.DocumentOperations, operations)
            .Add(x => x.BindPrompt, (_, _) => Task.FromResult<WorkflowPromptBindingOutcome>(new WorkflowPromptBindingOutcome.Rejected("Not used")))
            .Add(x => x.PreviewOperations, preview ?? new(_ => WorkflowPreviewRequirements.Empty,
                _ => Task.FromResult<IReadOnlyList<WorkflowPreviewProject>>([]),
                (_, _, _) => Task.FromResult<WorkflowPreviewOutcome>(new WorkflowPreviewOutcome.Rejected("Not used"))))
            .Add(x => x.DefinitionSaved, _ => accepted?.Invoke()));

    private static Task<WorkflowValidationResult> Valid(WorkflowDefinition _, CancellationToken token)
        => Task.FromResult(WorkflowValidationResult.Success);

    private static WorkflowDefinition Definition()
        => WorkflowCanvasDefinitionMapper.ToDefinition(WorkflowCanvasDefinitionMapper.CreateDraft([]));

    private static WorkflowDefinition Accept(WorkflowDefinition original, WorkflowDefinitionSaveRequest request)
        => original with { VersionId = WorkflowVersionId.New(), Name = request.Name, Description = request.Description,
            Graph = request.Graph, RuntimePolicy = request.RuntimePolicy, UpdatedAtUtc = DateTimeOffset.UtcNow };
}
