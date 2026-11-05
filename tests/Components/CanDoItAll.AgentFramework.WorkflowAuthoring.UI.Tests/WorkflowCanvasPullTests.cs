using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using static CanDoItAll.Tests.Components.WorkflowAuthoring.WorkflowCanvasAcceptanceTests;

namespace CanDoItAll.Tests.Components.WorkflowAuthoring;

public sealed class WorkflowCanvasPullTests {
    [Fact]
    public async Task Cancelling_a_held_comparison_refresh_does_not_reopen_the_dialog() {
        await using var context = CreateContext();
        var original = Definition();
        var calls = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<WorkflowDefinition?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = Render(context, original, new((_, _) => throw new InvalidOperationException(), Valid) {
            ReadCurrent = (_, _) => {
                if (++calls == 1) {
                    return Task.FromResult<WorkflowDefinition?>(original with { VersionId = WorkflowVersionId.New() });
                }
                entered.TrySetResult();
                return release.Task;
            }
        });
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-check-current']").ClickAsync());
        var refresh = cut.InvokeAsync(() => cut.Find("[data-testid='workflow-definition-review-refresh']").ClickAsync());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-definition-review-cancel']").ClickAsync());
        release.SetResult(original with { VersionId = WorkflowVersionId.New(), Name = "Late current" });
        await refresh;
        Assert.Empty(cut.FindAll("[data-testid='workflow-definition-review-dialog']"));
        Assert.Equal(original.Name, cut.Find("[data-testid='workflow-canvas-name']").GetAttribute("value"));
    }

    [Fact]
    public async Task A_native_access_refusal_keeps_the_draft_and_disables_further_remote_operations() {
        await using var context = CreateContext();
        var saves = 0;
        var cut = Render(context, Definition(), new((_, _) => {
            saves++;
            return Task.FromResult<WorkflowDefinitionSaveOutcome>(new WorkflowDefinitionSaveOutcome.Rejected(WorkflowSaveRejection.Access));
        }, Valid) { ReadCurrent = (_, _) => throw new InvalidOperationException("Retired reads must not run") });
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-name']").Change("Retained local access draft"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        Assert.Equal("Retained local access draft", cut.Find("[data-testid='workflow-canvas-name']").GetAttribute("value"));
        Assert.True(cut.Find("[data-testid='workflow-canvas-save']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='workflow-canvas-check-current']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='workflow-canvas-run-preview']").HasAttribute("disabled"));
        Assert.Equal(1, saves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_or_deleted_current_read_keeps_the_local_draft_without_saving(bool deleted) {
        await using var context = CreateContext();
        var saves = 0;
        var original = Definition();
        var cut = Render(context, original, new((request, _) => {
            saves++;
            return Task.FromResult<WorkflowDefinitionSaveOutcome>(new WorkflowDefinitionSaveOutcome.Accepted(Accept(original, request)));
        }, Valid) {
            ReadCurrent = (_, _) => deleted ? Task.FromResult<WorkflowDefinition?>(null)
                : Task.FromException<WorkflowDefinition?>(new InvalidOperationException("Private read failure"))
        });
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-name']").Change("Retained local"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-check-current']").ClickAsync());
        Assert.Equal("Retained local", cut.Find("[data-testid='workflow-canvas-name']").GetAttribute("value"));
        Assert.Empty(cut.FindAll("[data-testid='workflow-definition-review-dialog']"));
        Assert.Equal(0, saves);
        Assert.DoesNotContain("Private read failure", cut.Markup);
        Assert.Contains(deleted ? "deleted" : "could not be read", cut.Find("[data-testid='workflow-canvas-operation-error']").TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Applying_a_review_updates_only_the_draft_and_next_save_uses_the_reviewed_current_version() {
        await using var context = CreateContext();
        var original = Definition();
        var current = original with { VersionId = WorkflowVersionId.New(), Description = "Current description" };
        var requests = new List<WorkflowDefinitionSaveRequest>();
        var cut = Render(context, original, new((request, _) => {
            requests.Add(request);
            return Task.FromResult<WorkflowDefinitionSaveOutcome>(new WorkflowDefinitionSaveOutcome.Accepted(Accept(current, request)));
        }, Valid) { ReadCurrent = (_, _) => Task.FromResult<WorkflowDefinition?>(current) });
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-name']").Change("Local name"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-check-current']").ClickAsync());
        cut.WaitForElement("[data-testid='workflow-definition-review-dialog']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-definition-review-apply']").ClickAsync());
        Assert.Empty(requests);
        Assert.Equal("Local name", cut.Find("[data-testid='workflow-canvas-name']").GetAttribute("value"));
        Assert.Equal("Current description", cut.Find("[data-testid='workflow-canvas-description']").GetAttribute("value"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        var request = Assert.Single(requests);
        Assert.Equal(current.VersionId, request.ExpectedVersionId);
        Assert.Equal("Local name", request.Name);
        Assert.Equal("Current description", request.Description);
    }

    [Fact]
    public async Task Cancel_discards_only_review_choices_and_keeps_the_original_save_witness() {
        await using var context = CreateContext();
        var original = Definition();
        var current = original with { VersionId = WorkflowVersionId.New(), Name = "Current name" };
        WorkflowDefinitionSaveRequest? saved = null;
        var cut = Render(context, original, new((request, _) => {
            saved = request;
            return Task.FromResult<WorkflowDefinitionSaveOutcome>(new WorkflowDefinitionSaveOutcome.Rejected(WorkflowSaveRejection.Conflict));
        }, Valid) { ReadCurrent = (_, _) => Task.FromResult<WorkflowDefinition?>(current) });
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-name']").Change("Local name"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-check-current']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-definition-review-result-name']").Change("Review-only result"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-definition-review-cancel']").ClickAsync());
        Assert.Equal("Local name", cut.Find("[data-testid='workflow-canvas-name']").GetAttribute("value"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        Assert.NotNull(saved);
        Assert.Equal(original.VersionId, saved.ExpectedVersionId);
        Assert.Equal("Local name", saved.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_held_current_read_cannot_replace_later_edits_or_a_replacement_document(bool replaceDocument) {
        await using var context = CreateContext();
        var original = Definition();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<WorkflowDefinition?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = Render(context, original, new((_, _) => throw new InvalidOperationException(), Valid) {
            ReadCurrent = (_, _) => {
                entered.TrySetResult();
                return release.Task;
            }
        });
        var read = cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-check-current']").ClickAsync());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (replaceDocument) {
            await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-new-draft']").ClickAsync());
        }
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-name']").Change("Later local draft"));
        release.SetResult(original with { VersionId = WorkflowVersionId.New(), Name = "Remote name" });
        await read;
        Assert.Equal("Later local draft", cut.Find("[data-testid='workflow-canvas-name']").GetAttribute("value"));
        Assert.Empty(cut.FindAll("[data-testid='workflow-definition-review-dialog']"));
    }

    [Fact]
    public async Task A_later_field_edit_invalidates_an_open_review_without_advancing_the_original() {
        await using var context = CreateContext();
        var original = Definition();
        var current = original with { VersionId = WorkflowVersionId.New(), Description = "Remote description" };
        var cut = Render(context, original, new((_, _) => throw new InvalidOperationException(), Valid) {
            ReadCurrent = (_, _) => Task.FromResult<WorkflowDefinition?>(current)
        });
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-check-current']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-name']").Change("Later local draft"));
        Assert.True(cut.Find("[data-testid='workflow-definition-review-apply']").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-definition-review-cancel']").ClickAsync());
        Assert.Equal("Later local draft", cut.Find("[data-testid='workflow-canvas-name']").GetAttribute("value"));
        Assert.Equal(original.Description, cut.Find("[data-testid='workflow-canvas-description']").GetAttribute("value"));
    }

    [Fact]
    public async Task A_current_read_cannot_clear_an_unknown_save_or_enable_replay() {
        await using var context = CreateContext();
        var original = Definition();
        var saves = 0;
        var cut = Render(context, original, new((_, _) => {
            saves++;
            return Task.FromResult<WorkflowDefinitionSaveOutcome>(new WorkflowDefinitionSaveOutcome.Unknown());
        }, Valid) { ReadCurrent = (_, _) => Task.FromResult<WorkflowDefinition?>(original with { VersionId = WorkflowVersionId.New() }) });
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-save']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-canvas-check-current']").ClickAsync());
        Assert.True(cut.Find("[data-testid='workflow-definition-review-apply']").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-definition-review-cancel']").ClickAsync());
        Assert.True(cut.Find("[data-testid='workflow-canvas-save']").HasAttribute("disabled"));
        Assert.Equal(1, saves);
    }
}
