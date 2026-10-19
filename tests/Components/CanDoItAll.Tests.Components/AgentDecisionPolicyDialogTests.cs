using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Catalog;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentDecisionPolicyDialogTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delayed_save_preserves_typing_and_reopened_editor_observes_the_original_outcome(bool unknown) {
        await using var context = Context(out var proxy, out var state);
        var cut = context.Render<AgentDecisionPolicyDialog>(p => p.Add(x => x.State, state).Add(x => x.AgentName, "Selected policy Agent"));
        cut.WaitForElement("[data-testid='agent-policy-read']");
        Assert.Contains("Selected policy Agent", cut.Markup, StringComparison.Ordinal);
        await cut.InvokeAsync(() => cut.Find("[data-testid='agent-policy-read']").Change("RunSnapshot"));
        var pending = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        await proxy.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await cut.InvokeAsync(() => cut.Find("[data-testid='agent-policy-work']").Input("00:04:00"));
        Assert.Equal(ReadBasisMode.RunSnapshot, proxy.Submitted!.Policy.ReadBasisMode);
        Assert.Null(proxy.Submitted.Policy.MaximumWorkDuration);
        await context.DisposeRenderedComponentsAsync();
        var reopened = context.Render<AgentDecisionPolicyDialog>(p => p.Add(x => x.State, state));
        Assert.True(reopened.Find("[data-testid='agent-policy-save']").HasAttribute("disabled"));
        var command = proxy.Submitted;
        proxy.Completion.SetResult(unknown ? new AgentDecisionDataPolicySaveOutcome.Unconfirmed(command)
            : new AgentDecisionDataPolicySaveOutcome.Accepted(command,
                new(new(command.Expectation.Workspace, command.Expectation.AgentId, command.Expectation.CatalogRevision.Next()), command.Policy)));
        await pending.WaitAsync(TimeSpan.FromSeconds(10));
        reopened.WaitForAssertion(() => {
            Assert.False(state.Busy);
            Assert.Equal("00:04:00", reopened.Find("[data-testid='agent-policy-work']").GetAttribute("value"));
            Assert.Equal(unknown, reopened.Find("[data-testid='agent-policy-save']").HasAttribute("disabled"));
        });
        Assert.Equal(1, proxy.Writes);
        if (unknown) {
            await reopened.InvokeAsync(() => reopened.Find("[data-testid='agent-policy-read-current']").ClickAsync(new MouseEventArgs()));
            reopened.WaitForElement("[data-testid='agent-policy-comparison']");
            Assert.True(reopened.Find("[data-testid='agent-policy-keep-draft']").HasAttribute("disabled"));
            Assert.Equal(command, state.Unconfirmed);
        }
    }

    [Fact]
    public async Task Invalid_numeric_text_cannot_submit_a_stale_last_valid_value() {
        await using var context = Context(out var proxy, out var state);
        var cut = context.Render<AgentDecisionPolicyDialog>(p => p.Add(x => x.State, state));
        cut.WaitForElement("[data-testid='agent-policy-replans']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='agent-policy-replans']").Change("invalid"));
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        Assert.Equal(0, proxy.Writes);
        Assert.Contains("must be a number", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Retired_profile_blocks_further_intents_and_keeps_the_old_submission_bound() {
        await using var context = Context(out var proxy, out var state);
        using var lifetime = new CancellationTokenSource();
        var callbacks = 0;
        var cut = context.Render<AgentDecisionPolicyDialog>(p => p.Add(x => x.State, state)
            .Add(x => x.OwnerLifetime, lifetime.Token).Add(x => x.Saved, _ => callbacks++));
        cut.WaitForElement("[data-testid='agent-policy-save']");
        var pending = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        await proxy.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        lifetime.Cancel();
        await cut.InvokeAsync(state.Retire);
        var command = proxy.Submitted!;
        proxy.Completion.SetResult(new AgentDecisionDataPolicySaveOutcome.Accepted(command,
            new(new(command.Expectation.Workspace, command.Expectation.AgentId, command.Expectation.CatalogRevision.Next()), command.Policy)));
        await pending.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, callbacks);
        Assert.False(state.CanSave);
        Assert.Equal(command.Expectation.Workspace, state.Original!.Expectation.Workspace);
        Assert.Equal(1, proxy.Writes);
    }

    [Theory]
    [InlineData(PendingFailure.Initialize)]
    [InlineData(PendingFailure.Prepare)]
    [InlineData(PendingFailure.DispatchReply)]
    public async Task Storage_failure_never_dispatches_and_recovery_does_not_resend(PendingFailure failure) {
        await using var context = Context(out var proxy, out var state);
        var store = context.Services.GetRequiredService<PendingStore>();
        store.Failure = failure;
        var cut = context.Render<AgentDecisionPolicyDialog>(p => p.Add(x => x.State, state));
        if (failure != PendingFailure.Initialize) {
            cut.WaitForAssertion(() => Assert.True(state.CanSave));
            await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        }
        cut.WaitForAssertion(() => Assert.True(state.RecoveryBlocked));
        Assert.Equal(0, proxy.Writes);
        Assert.False(state.CanSave);
        store.Failure = PendingFailure.None;
        await cut.InvokeAsync(() => cut.Find("[data-testid='agent-policy-recover']").ClickAsync(new MouseEventArgs()));
        Assert.Equal(0, proxy.Writes);
        if (failure == PendingFailure.DispatchReply) {
            Assert.Equal(AgentDecisionPolicyPendingStage.Dispatching, state.Pending!.Stage);
            Assert.Equal(1, proxy.Lookups);
            Assert.Contains("does not prove", cut.Markup, StringComparison.Ordinal);
            Assert.False(state.CanSave);
        }
    }

    [Fact]
    public async Task Reload_restores_original_command_across_generation_and_lookup_preserves_later_typing() {
        await using var context = Context(out var proxy, out var state);
        var cut = context.Render<AgentDecisionPolicyDialog>(p => p.Add(x => x.State, state));
        cut.WaitForAssertion(() => Assert.True(state.CanSave));
        await cut.InvokeAsync(() => cut.Find("[data-testid='agent-policy-work']").Input("00:03:00"));
        var save = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        await proxy.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var command = proxy.Submitted!;
        proxy.Completion.SetResult(new AgentDecisionDataPolicySaveOutcome.Unconfirmed(command));
        await save;
        await context.DisposeRenderedComponentsAsync();
        proxy.Workspace = new(proxy.Workspace.DatabaseProfileId, proxy.Workspace.WorkspaceScope, new(2));
        state = new(proxy.AgentId);
        var restored = context.Render<AgentDecisionPolicyDialog>(p => p.Add(x => x.State, state));
        restored.WaitForAssertion(() => Assert.Equal(command, state.Pending?.Command));
        Assert.Equal("00:03:00", state.Draft.MaximumWorkDuration);
        Assert.NotEqual(command.Expectation.Workspace, state.Original!.Expectation.Workspace);
        await restored.InvokeAsync(() => restored.Find("[data-testid='agent-policy-work']").Input("still typing"));
        proxy.LookupResult = new AgentDecisionDataPolicyLookup.Accepted(new(command, DateTimeOffset.UtcNow));
        await restored.InvokeAsync(() => restored.Find("[data-testid='agent-policy-recover']").ClickAsync(new MouseEventArgs()));
        Assert.Equal(command, proxy.LookedUp);
        Assert.Equal(Accepted(command).Snapshot, state.Historical);
        Assert.Equal("still typing", state.Draft.MaximumWorkDuration);
        Assert.False(state.CanSave);
        Assert.Null(context.Services.GetRequiredService<PendingStore>().Record);
        await restored.InvokeAsync(() => restored.Find("[data-testid='agent-policy-read-current']").ClickAsync(new MouseEventArgs()));
        await restored.InvokeAsync(() => restored.Find("[data-testid='agent-policy-keep-draft']").ClickAsync(new MouseEventArgs()));
        Assert.True(state.CanSave);
        Assert.Equal("still typing", state.Draft.MaximumWorkDuration);
        Assert.Equal(1, proxy.Writes);
    }

    [Theory]
    [InlineData(PendingFailure.RecordOutcome)]
    [InlineData(PendingFailure.ClearOutcome)]
    public async Task Accepted_cleanup_failure_recovers_without_replacing_later_draft(PendingFailure failure) {
        await using var context = Context(out var proxy, out var state);
        var store = context.Services.GetRequiredService<PendingStore>();
        store.Failure = failure;
        var cut = context.Render<AgentDecisionPolicyDialog>(p => p.Add(x => x.State, state));
        cut.WaitForAssertion(() => Assert.True(state.CanSave));
        var save = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        await proxy.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var command = proxy.Submitted!;
        await cut.InvokeAsync(() => cut.Find("[data-testid='agent-policy-work']").Input("00:07:00"));
        proxy.Completion.SetResult(new AgentDecisionDataPolicySaveOutcome.Accepted(command, Accepted(command).Snapshot, true));
        await save;
        Assert.True(state.RecoveryBlocked);
        Assert.False(state.CanSave);
        Assert.NotNull(store.Record);
        store.Failure = PendingFailure.None;
        proxy.LookupResult = new AgentDecisionDataPolicyLookup.Accepted(new(command, DateTimeOffset.UtcNow));
        await cut.InvokeAsync(() => cut.Find("[data-testid='agent-policy-recover']").ClickAsync(new MouseEventArgs()));
        Assert.Equal(Accepted(command).Snapshot, state.Historical);
        Assert.Equal("00:07:00", state.Draft.MaximumWorkDuration);
        Assert.Contains("follow-up failure", state.Message, StringComparison.Ordinal);
        Assert.False(state.CanSave);
        Assert.Null(store.Record);
        Assert.Equal(0, proxy.Lookups);
        Assert.Equal(1, proxy.Writes);
    }

    [Fact]
    public async Task Prepared_record_can_be_discarded_but_a_dispatching_record_cannot() {
        await using var context = Context(out var proxy, out var state);
        var store = context.Services.GetRequiredService<PendingStore>();
        var command = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), new(proxy.Workspace, proxy.AgentId, new(4)), new(MaximumReplans: 2));
        store.Record = new(command, AgentDecisionPolicyPendingStage.Prepared);
        var cut = context.Render<AgentDecisionPolicyDialog>(p => p.Add(x => x.State, state));
        cut.WaitForElement("[data-testid='agent-policy-discard-prepared']");
        Assert.Equal(2, state.Draft.MaximumReplans);
        store.Record = new(command, AgentDecisionPolicyPendingStage.Dispatching);
        await cut.InvokeAsync(() => cut.Find("[data-testid='agent-policy-discard-prepared']").ClickAsync(new MouseEventArgs()));
        Assert.Equal(command, state.Pending?.Command);
        Assert.False(state.CanDiscardPrepared);
        Assert.False(state.CanSave);
        Assert.Equal(0, proxy.Writes);
        await context.DisposeRenderedComponentsAsync();
        store.Record = new(command, AgentDecisionPolicyPendingStage.Prepared);
        state = new(proxy.AgentId);
        var reopened = context.Render<AgentDecisionPolicyDialog>(p => p.Add(x => x.State, state));
        reopened.WaitForElement("[data-testid='agent-policy-discard-prepared']");
        await reopened.InvokeAsync(() => reopened.Find("[data-testid='agent-policy-discard-prepared']").ClickAsync(new MouseEventArgs()));
        Assert.Null(store.Record);
        Assert.Null(state.Pending);
        Assert.True(state.NeedsReview);
        Assert.Equal(2, state.Draft.MaximumReplans);
    }

    [Fact]
    public async Task Another_tabs_operation_survives_late_acknowledgement_and_recovery_holds_the_busy_gate() {
        await using var context = Context(out var proxy, out var state);
        var store = context.Services.GetRequiredService<PendingStore>();
        var cut = context.Render<AgentDecisionPolicyDialog>(p => p.Add(x => x.State, state));
        cut.WaitForAssertion(() => Assert.True(state.CanSave));
        var save = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        await proxy.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var original = proxy.Submitted!;
        var newer = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), Accepted(original).Snapshot.Expectation, new(MaximumReplans: 3));
        store.Record = new(newer, AgentDecisionPolicyPendingStage.Dispatching);
        proxy.Completion.SetResult(Accepted(original));
        await save;
        Assert.Equal(newer, store.Record.Command);
        Assert.True(state.RecoveryBlocked);
        proxy.LookupEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        proxy.LookupCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovery = cut.InvokeAsync(() => cut.Find("[data-testid='agent-policy-recover']").ClickAsync(new MouseEventArgs()));
        await proxy.LookupEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(state.Busy);
        Assert.False(state.CanSave);
        Assert.Equal(newer, proxy.LookedUp);
        proxy.LookupCompletion.SetResult(new AgentDecisionDataPolicyLookup.NotObserved());
        await recovery;
        Assert.Equal(newer, store.Record.Command);
        Assert.Equal(1, proxy.Writes);
    }

    private static AgentDecisionDataPolicySaveOutcome.Accepted Accepted(ReplaceAgentDecisionDataPolicyCommand command)
        => new(command, new(new(command.Expectation.Workspace, command.Expectation.AgentId, command.Expectation.CatalogRevision.Next()), command.Policy));

    private static BunitContext Context(out PolicyWorkspace proxy, out AgentDecisionPolicyEditorState state) {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, PolicyWorkspace>();
        proxy = (PolicyWorkspace)(object)workspace;
        state = new(proxy.AgentId);
        context.Services.AddSingleton(workspace);
        context.Services.AddSingleton<PendingStore>();
        context.Services.AddSingleton<IAgentDecisionPolicyPendingStore>(services => services.GetRequiredService<PendingStore>());
        return context;
    }

    public class PolicyWorkspace : DispatchProxy {
        public Guid AgentId { get; } = Guid.NewGuid();
        public AgentExecutionActivityWorkspaceIdentity Workspace { get; set; } = new(Guid.NewGuid(), WorkspaceScopeDescriptor.Sandbox, new(1));
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<AgentDecisionDataPolicySaveOutcome> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ReplaceAgentDecisionDataPolicyCommand? Submitted { get; private set; }
        public int Writes { get; private set; }
        public int Lookups { get; private set; }
        public ReplaceAgentDecisionDataPolicyCommand? LookedUp { get; private set; }
        public AgentDecisionDataPolicyLookup LookupResult { get; set; } = new AgentDecisionDataPolicyLookup.NotObserved();
        public TaskCompletionSource? LookupEntered { get; set; }
        public TaskCompletionSource<AgentDecisionDataPolicyLookup>? LookupCompletion { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.ReadAgentDecisionDataPolicyAsync)) {
                return Task.FromResult(new AgentDecisionDataPolicySnapshot(new(Workspace, AgentId, new(4)), null));
            }
            if (targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.ReplaceAgentDecisionDataPolicyAsync)) {
                Submitted = (ReplaceAgentDecisionDataPolicyCommand)args![0]!;
                Writes++;
                Entered.TrySetResult();
                return Completion.Task;
            }
            if (targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.LookupAgentDecisionDataPolicyAsync)) {
                LookedUp = (ReplaceAgentDecisionDataPolicyCommand)args![0]!;
                Lookups++;
                LookupEntered?.TrySetResult();
                return LookupCompletion?.Task ?? Task.FromResult(LookupResult);
            }
            throw new InvalidOperationException("Unexpected policy editor dispatch.");
        }
    }

    public enum PendingFailure { None, Initialize, Prepare, DispatchReply, RecordOutcome, ClearOutcome }

    private sealed class PendingStore : IAgentDecisionPolicyPendingStore {
        public AgentDecisionPolicyPendingRecord? Record { get; set; }
        public PendingFailure Failure { get; set; }
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) {
            Fail(PendingFailure.Initialize);
            return ValueTask.CompletedTask;
        }
        public ValueTask<AgentDecisionPolicyPendingRecord?> ReadAsync(AgentDecisionDataPolicyExpectation context, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Record);
        public ValueTask<AgentDecisionPolicyPendingChange> PrepareAsync(ReplaceAgentDecisionDataPolicyCommand command, CancellationToken cancellationToken = default) {
            Fail(PendingFailure.Prepare);
            if (Record is not null) {
                return Result(false);
            }
            Record = new(command, AgentDecisionPolicyPendingStage.Prepared);
            return Result(true);
        }
        public ValueTask<AgentDecisionPolicyPendingChange> BeginDispatchAsync(ReplaceAgentDecisionDataPolicyCommand command, CancellationToken cancellationToken = default) {
            if (Record?.Command != command || Record.Stage != AgentDecisionPolicyPendingStage.Prepared) {
                return Result(false);
            }
            Record = new(command, AgentDecisionPolicyPendingStage.Dispatching);
            Fail(PendingFailure.DispatchReply);
            return Result(true);
        }
        public ValueTask<AgentDecisionPolicyPendingChange> RecordOutcomeAsync(ReplaceAgentDecisionDataPolicyCommand command,
            AgentDecisionDataPolicySaveOutcome outcome, CancellationToken cancellationToken = default) {
            Fail(PendingFailure.RecordOutcome);
            if (Record?.Command != command) {
                return Result(false);
            }
            Record = new(command, outcome is AgentDecisionDataPolicySaveOutcome.Accepted ? AgentDecisionPolicyPendingStage.Accepted : AgentDecisionPolicyPendingStage.Rejected, outcome);
            return Result(true);
        }
        public ValueTask<AgentDecisionPolicyPendingChange> ClearResolvedAsync(ReplaceAgentDecisionDataPolicyCommand command, CancellationToken cancellationToken = default) {
            Fail(PendingFailure.ClearOutcome);
            return Remove(command, Record?.Stage is AgentDecisionPolicyPendingStage.Accepted or AgentDecisionPolicyPendingStage.Rejected);
        }
        public ValueTask<AgentDecisionPolicyPendingChange> CancelPreparedAsync(ReplaceAgentDecisionDataPolicyCommand command, CancellationToken cancellationToken = default)
            => Remove(command, Record?.Stage == AgentDecisionPolicyPendingStage.Prepared);
        private ValueTask<AgentDecisionPolicyPendingChange> Remove(ReplaceAgentDecisionDataPolicyCommand command, bool allowed) {
            var changed = allowed && Record?.Command == command;
            if (changed) {
                Record = null;
            }
            return Result(changed);
        }
        private ValueTask<AgentDecisionPolicyPendingChange> Result(bool changed) => ValueTask.FromResult(new AgentDecisionPolicyPendingChange(changed, Record));
        private void Fail(PendingFailure stage) {
            if (Failure == stage) {
                throw new IOException("Injected recovery storage failure.");
            }
        }
    }
}
