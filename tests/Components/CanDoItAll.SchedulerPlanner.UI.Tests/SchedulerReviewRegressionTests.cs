using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.SchedulerPlanner.Presentation;
using CanDoItAll.SchedulerPlanner.UI;
using CanDoItAll.SchedulerPlanner.UiSandbox;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.SchedulerPlanner;

public sealed class SchedulerReviewRegressionTests : BunitContext {
    public SchedulerReviewRegressionTests() {
        Services.AddCanDoItAllBaseLib();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData("success")]
    [InlineData("mismatch")]
    [InlineData("failure")]
    [InlineData("disposed")]
    public async Task Old_save_review_cannot_replace_successor_or_erase_its_recovery(string completion) {
        var owner = new ReviewOwner();
        using var workspace = new SchedulerWorkspace(owner);
        await workspace.InitializeAsync();
        await workspace.EditAsync(SchedulerScenarioStore.PlanA);
        var draft = workspace.EditDraft!;
        await workspace.SaveAsync(draft);
        var old = Signal<SchedulerDraftValues>();
        var newer = Signal<SchedulerDraftValues>();
        var reads = new Queue<Task<SchedulerDraftValues>>([old.Task, newer.Task]);
        owner.Editor = (_, _) => reads.Dequeue();
        var first = workspace.ReviewUnknownAsync(draft, SchedulerScenarioStore.PlanA);
        var second = workspace.ReviewUnknownAsync(draft, SchedulerScenarioStore.PlanA);
        newer.SetResult(draft.Values);
        await second.WaitAsync(TimeSpan.FromSeconds(5));
        var validation = Signal<SchedulerWorkflowInputValidationResult>();
        owner.Store.ValidationOverride = (_, _) => validation.Task;
        var saving = workspace.SaveAsync(draft);
        var pending = draft.Receipt;
        Assert.Equal(SchedulerMutationStatus.Pending, pending!.Status);
        if (completion == "disposed") {
            workspace.Dispose();
        }
        if (completion == "failure") {
            old.SetException(new IOException("Retired review error"));
        } else {
            old.SetResult(completion == "mismatch" ? draft.Values with { TargetId = Guid.NewGuid() } : draft.Values);
        }
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(pending, draft.Receipt);
        Assert.Same(pending, workspace.PlanReceipts[SchedulerScenarioStore.PlanA]);
        Assert.Empty(draft.Error);
        await workspace.SaveAsync(draft);
        var plan = workspace.Data!.Plans.Single(item => item.Id == SchedulerScenarioStore.PlanA);
        await workspace.ToggleAsync(plan);
        workspace.AskDelete(plan);
        await workspace.DeleteAsync();
        Assert.Equal(1, owner.Store.Writes);
        validation.SetResult(new(true, draft.Values.InputJson, []));
        await saving.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(SchedulerMutationStatus.Unknown, draft.Receipt!.Status);
        if (completion != "disposed") {
            owner.Editor = (id, token) => owner.Store.EditorAsync(id, token);
            await workspace.ReviewUnknownAsync(draft, SchedulerScenarioStore.PlanA);
            Assert.Equal(SchedulerMutationStatus.CommittedWithWarning, draft.Receipt!.Status);
            await workspace.ToggleAsync(workspace.Data.Plans.Single(item => item.Id == SchedulerScenarioStore.PlanB));
            Assert.Equal(3, owner.Store.Writes);
        }
    }

    [Theory]
    [InlineData(false, "success")]
    [InlineData(false, "mismatch")]
    [InlineData(false, "failure")]
    [InlineData(false, "disposed")]
    [InlineData(true, "success")]
    [InlineData(true, "mismatch")]
    [InlineData(true, "failure")]
    [InlineData(true, "disposed")]
    public async Task Old_plan_review_cannot_replace_successor(bool delete, string completion) {
        var owner = new ReviewOwner { UnknownPlans = true };
        using var workspace = new SchedulerWorkspace(owner);
        await workspace.InitializeAsync();
        var plan = workspace.Data!.Plans.Single(item => item.Id == SchedulerScenarioStore.PlanA);
        if (delete) {
            workspace.AskDelete(plan);
            await workspace.DeleteAsync();
        } else {
            await workspace.ToggleAsync(plan);
        }
        var old = Signal<SchedulerDraftValues>();
        var newer = Signal<SchedulerDraftValues>();
        var reads = new Queue<Task<SchedulerDraftValues>>([old.Task, newer.Task]);
        owner.Editor = (_, _) => reads.Dequeue();
        var first = workspace.ReviewPlanAsync(plan.Id);
        var second = workspace.ReviewPlanAsync(plan.Id);
        if (delete) {
            newer.SetException(new KeyNotFoundException());
        } else {
            newer.SetResult(owner.Store.Plans[plan.Id] with { IsEnabled = false });
        }
        await second.WaitAsync(TimeSpan.FromSeconds(5));
        owner.UnknownPlans = false;
        owner.Store.Hold = SchedulerWait.Save;
        var saving = workspace.ToggleAsync(plan);
        await owner.Store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var pending = workspace.PlanReceipts[plan.Id];
        if (completion == "disposed") {
            workspace.Dispose();
        }
        if (completion == "failure") {
            old.SetException(new IOException("Retired plan review error"));
        } else if (delete && completion != "mismatch") {
            old.SetException(new KeyNotFoundException());
        } else {
            old.SetResult(owner.Store.Plans[plan.Id] with { IsEnabled = completion == "mismatch" });
        }
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(pending, workspace.PlanReceipts[plan.Id]);
        owner.Store.Release();
        await saving.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Parent_control_retires_only_removed_child_diagnostics_and_revalidates(bool required) {
        var store = new SchedulerScenarioStore();
        using var workspace = new SchedulerWorkspace(store);
        await workspace.InitializeAsync();
        workspace.Tab = SchedulerTab.NewSchedule;
        var draft = workspace.NewDraft;
        var calls = 0;
        store.ValidationOverride = (values, _) => {
            calls++;
            return Task.FromResult(calls == 1 || required
                ? new SchedulerWorkflowInputValidationResult(false, values.InputJson, [new("node", "Node is invalid or required.")])
                : new(true, values.InputJson, []));
        };
        await workspace.SetRawInputAsync(draft, "{\"project\":\"old\",\"node\":\"bad\",\"vendor\":true}");
        var cut = Render<SchedulerWorkspaceSurface>(p => p.Add(x => x.Workspace, workspace));
        await cut.Find("[data-testid=scheduler-save]").ClickAsync(new());
        Assert.Single(draft.Issues);
        var options = store.OptionReads;
        await cut.Find("[data-testid=scheduler-input-project]").ChangeAsync(new() { Value = "project-a" });
        Assert.Empty(draft.Issues);
        Assert.DoesNotContain("node", draft.Values.InputJson);
        await cut.Find("[data-testid=scheduler-save]").ClickAsync(new());
        Assert.Equal(2, calls);
        Assert.Equal(required ? 0 : 1, store.Writes);
        Assert.Equal(options + 1, store.OptionReads);
        await cut.Find("[data-testid=scheduler-input-minutes]").InputAsync(new() { Value = "-" });
        await cut.Find("[data-testid=scheduler-input-project]").ChangeAsync(new() { Value = "third" });
        Assert.Contains(draft.Issues, issue => issue.ParameterKey == "minutes");
        await workspace.SaveAsync(draft);
        Assert.Equal(2, calls);
        await cut.Find("[data-testid=scheduler-input-json]").InputAsync(new() { Value = "{broken" });
        await cut.Find("[data-testid=scheduler-input-project]").ChangeAsync(new() { Value = "fourth" });
        Assert.Contains(draft.Issues, issue => issue.ParameterKey.Length == 0);
        await workspace.SaveAsync(draft);
        Assert.Equal(2, calls);
    }

    private static TaskCompletionSource<T> Signal<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task Recovery_control_tracks_its_current_read() {
        var owner = new ReviewOwner();
        using var workspace = new SchedulerWorkspace(owner);
        await workspace.InitializeAsync();
        await workspace.EditAsync(SchedulerScenarioStore.PlanA);
        var draft = workspace.EditDraft!;
        await workspace.SaveAsync(draft);
        var held = Signal<SchedulerDraftValues>();
        owner.Editor = (_, _) => held.Task;
        var cut = Render<SchedulerWorkspaceSurface>(p => p.Add(x => x.Workspace, workspace));
        await cut.Find("[data-testid=scheduler-review-id]").InputAsync(new() { Value = SchedulerScenarioStore.PlanA.ToString() });
        var reviewing = cut.Find("[data-testid=scheduler-review-unknown]").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid=scheduler-review-unknown]").HasAttribute("disabled")));
        Assert.True(draft.IsReviewing);
        held.SetResult(draft.Values);
        await reviewing.WaitAsync(TimeSpan.FromSeconds(5));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=scheduler-review-unknown]")));
        Assert.False(draft.IsReviewing);
    }

    private sealed class ReviewOwner : ISchedulerWorkspaceOwner {
        public SchedulerScenarioStore Store { get; } = new(SchedulerScenario.UnknownSave);
        public Func<Guid, CancellationToken, Task<SchedulerDraftValues>>? Editor { get; set; }
        public bool UnknownPlans { get; set; }
        public Task<SchedulerWorkspaceData> ReadAsync(SchedulerHistoryQuery query, CancellationToken token) => Store.ReadAsync(query, token);
        public Task<SchedulerDraftValues> DefaultAsync(CancellationToken token) => Store.DefaultAsync(token);
        public Task<SchedulerDraftValues> EditorAsync(Guid id, CancellationToken token) => Editor?.Invoke(id, token) ?? Store.EditorAsync(id, token);
        public Task<SchedulerWorkflowInputSchema> SchemaAsync(SchedulerDraftValues values, CancellationToken token) => Store.SchemaAsync(values, token);
        public Task<IReadOnlyList<WorkflowInputParameterOption>> OptionsAsync(WorkflowInputParameterDescriptor parameter, IReadOnlyDictionary<string, string> values, CancellationToken token) => Store.OptionsAsync(parameter, values, token);
        public Task<SchedulerWorkflowInputValidationResult> ValidateAsync(SchedulerDraftValues values, CancellationToken token) => Store.ValidateAsync(values, token);
        public Task<SchedulerMutationReceipt> SaveAsync(SchedulerDraftValues values) => Store.SaveAsync(values);
        public Task<SchedulerMutationReceipt> ToggleAsync(Guid id, bool enabled) => UnknownPlans ? Task.FromResult(new SchedulerMutationReceipt(SchedulerMutationKind.Disable, SchedulerMutationStatus.Unknown, id, SchedulerMutationStage.None, "Unknown")) : Store.ToggleAsync(id, enabled);
        public Task<SchedulerMutationReceipt> DeleteAsync(Guid id) => UnknownPlans ? Task.FromResult(new SchedulerMutationReceipt(SchedulerMutationKind.Delete, SchedulerMutationStatus.Unknown, id, SchedulerMutationStage.None, "Unknown")) : Store.DeleteAsync(id);
        public string DescribeCron(string expression, string zone) => Store.DescribeCron(expression, zone);
    }
}
