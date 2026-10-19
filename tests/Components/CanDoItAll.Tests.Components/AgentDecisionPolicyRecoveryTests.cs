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
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentDecisionPolicyRecoveryTests {
    [Fact]
    public async Task Failed_list_read_renders_the_actual_recovery_error() {
        await using var context = Context(out _, out var pending);
        pending.AfterList = () => throw new IOException("Injected unavailable storage.");
        var list = context.Render<AgentDecisionPolicyRecoveryDialog>();
        var error = list.WaitForElement("[data-testid='agent-policy-recovery-error']");
        Assert.Contains("Retained saves could not be read", error.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("session.Error", list.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deleted_agent_opens_from_retention_and_recovers_without_a_current_read_or_write() {
        await using var context = Context(out var owner, out var pending);
        var command = pending.Record!.Command;
        owner.Lookup = new AgentDecisionDataPolicyLookup.Accepted(new(command, DateTimeOffset.UtcNow));
        AgentDecisionPolicyRecoverySelection? selected = null;
        var list = context.Render<AgentDecisionPolicyRecoveryDialog>(p => p.Add(x => x.Selected, value => selected = value));
        var row = list.WaitForElement($"button[data-testid='agent-policy-recovery-{command.Expectation.AgentId:N}']");
        await list.InvokeAsync(() => row.ClickAsync(new MouseEventArgs()));
        Assert.NotNull(selected);
        var state = new AgentDecisionPolicyEditorState(command.Expectation.AgentId);
        state.RestorePending(selected.Context, selected.Record);
        Assert.Null(state.Original);
        Assert.False(state.CanSave);
        await context.DisposeRenderedComponentsAsync();
        var editor = context.Render<AgentDecisionPolicyDialog>(p => p.Add(x => x.State, state));
        Assert.Empty(editor.FindAll("form"));
        await editor.InvokeAsync(() => editor.Find("[data-testid='agent-policy-recover']").ClickAsync(new MouseEventArgs()));
        editor.WaitForElement("[data-testid='agent-policy-historical']");
        Assert.Null(state.Original);
        Assert.False(state.CanSave);
        Assert.Null(pending.Record);
        Assert.Equal(command, owner.LookedUp);
        Assert.Equal(0, owner.CurrentReads);
        await editor.InvokeAsync(() => editor.Find("[data-testid='agent-policy-read-current']").ClickAsync(new MouseEventArgs()));
        Assert.Equal(1, owner.CurrentReads);
        Assert.NotNull(state.Historical);
        Assert.False(state.CanSave);
    }

    [Theory]
    [InlineData(SelectionChange.Removed)]
    [InlineData(SelectionChange.Operation)]
    [InlineData(SelectionChange.Stage)]
    [InlineData(SelectionChange.Scope)]
    [InlineData(SelectionChange.Generation)]
    public async Task Stale_selection_never_opens_another_submission(SelectionChange change) {
        await using var context = Context(out var owner, out var pending);
        var session = Session(context, pending);
        await session.ReadAsync(default);
        var row = Assert.Single(session.Rows);
        var command = pending.Record!.Command;
        switch (change) {
            case SelectionChange.Removed:
                pending.Record = null;
                break;
            case SelectionChange.Operation:
                pending.Record = new(new(Guid.NewGuid(), command.Expectation, command.Policy), pending.Record.Stage);
                break;
            case SelectionChange.Stage:
                pending.Record = new(command, AgentDecisionPolicyPendingStage.Prepared);
                break;
            case SelectionChange.Scope:
                pending.Record = new(new(command.OperationId, new(new(Guid.NewGuid(), WorkspaceScopeDescriptor.Sandbox, new(1)),
                    command.Expectation.AgentId, command.Expectation.CatalogRevision), command.Policy), pending.Record.Stage);
                break;
            case SelectionChange.Generation:
                owner.Identity = new(owner.Identity.DatabaseProfileId, owner.Identity.WorkspaceScope, new(2));
                break;
        }
        Assert.Null(await session.SelectAsync(row, default));
        Assert.NotNull(session.Error);
        Assert.Null(owner.LookedUp);
        Assert.Equal(0, owner.CurrentReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Context_change_or_retirement_during_list_read_cannot_publish_rows(bool retire) {
        await using var context = Context(out var owner, out var pending);
        var session = Session(context, pending);
        pending.AfterList = () => {
            if (retire) {
                session.Retire();
            } else {
                owner.Identity = new(Guid.NewGuid(), WorkspaceScopeDescriptor.Sandbox, new(1));
            }
        };
        await session.ReadAsync(default);
        Assert.Empty(session.Rows);
        Assert.Equal(!retire, session.Error is not null);
    }

    [Fact]
    public async Task Cancelled_or_retired_selection_cannot_reopen_the_dialog() {
        await using var context = Context(out _, out var pending);
        using var cancellation = new CancellationTokenSource();
        var session = Session(context, pending);
        await session.ReadAsync(default);
        var row = Assert.Single(session.Rows);
        pending.AfterRead = cancellation.Cancel;
        Assert.Null(await session.SelectAsync(row, cancellation.Token));
        Assert.Null(session.Error);
        pending.AfterRead = session.Retire;
        Assert.Null(await session.SelectAsync(row, default));
        Assert.Empty(session.Rows);
    }

    [Fact]
    public async Task Historical_generation_can_be_recovered_but_never_becomes_the_editing_baseline() {
        await using var context = Context(out var owner, out var pending);
        var command = pending.Record!.Command;
        owner.Identity = new(owner.Identity.DatabaseProfileId, owner.Identity.WorkspaceScope, new(9));
        var session = Session(context, pending);
        await session.ReadAsync(default);
        var selected = await session.SelectAsync(Assert.Single(session.Rows), default);
        Assert.NotNull(selected);
        var state = new AgentDecisionPolicyEditorState(command.Expectation.AgentId);
        state.RestorePending(selected.Context, selected.Record);
        Assert.False(state.CanSave);
        Assert.Equal(command, state.Unconfirmed);
        Assert.Throws<InvalidOperationException>(() => state.CompleteRead(new(command.Expectation, command.Policy)));
    }

    private static AgentDecisionPolicyRecoverySession Session(BunitContext context, PendingStore pending)
        => new(context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>(), pending, NullLogger<AgentDecisionPolicyRecoverySession>.Instance);

    private static BunitContext Context(out PolicyOwner owner, out PendingStore pending) {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, PolicyOwner>();
        owner = (PolicyOwner)(object)workspace;
        pending = new() { Record = new(new(Guid.NewGuid(), new(owner.Identity, Guid.NewGuid(), new(4)),
            new(ReadBasisMode.RunSnapshot, MaximumWorkDuration: TimeSpan.FromMinutes(3))), AgentDecisionPolicyPendingStage.Dispatching) };
        context.Services.AddSingleton(workspace);
        context.Services.AddSingleton<IAgentDecisionPolicyPendingStore>(pending);
        return context;
    }

    public enum SelectionChange { Removed, Operation, Stage, Scope, Generation }
    public class PolicyOwner : DispatchProxy {
        public AgentExecutionActivityWorkspaceIdentity Identity { get; set; } = new(Guid.NewGuid(), WorkspaceScopeDescriptor.Sandbox, new(1));
        public AgentDecisionDataPolicyLookup Lookup { get; set; } = new AgentDecisionDataPolicyLookup.NotObserved();
        public ReplaceAgentDecisionDataPolicyCommand? LookedUp { get; private set; }
        public int CurrentReads { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.ReadAgentDecisionDataPolicyContextAsync)) {
                return Task.FromResult(Identity);
            }
            if (targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.LookupAgentDecisionDataPolicyAsync)) {
                LookedUp = (ReplaceAgentDecisionDataPolicyCommand)args![0]!;
                return Task.FromResult(Lookup);
            }
            if (targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.ReadAgentDecisionDataPolicyAsync)) {
                CurrentReads++;
                return Task.FromException<AgentDecisionDataPolicySnapshot>(new InvalidOperationException("Agent no longer exists."));
            }
            throw new InvalidOperationException("Recovery must never write or load the live catalog.");
        }
    }
    private sealed class PendingStore : IAgentDecisionPolicyPendingStore {
        public AgentDecisionPolicyPendingRecord? Record { get; set; }
        public Action? AfterList { get; set; }
        public Action? AfterRead { get; set; }
        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<IReadOnlyList<AgentDecisionPolicyPendingSummary>> ListAsync(AgentExecutionActivityWorkspaceIdentity context, CancellationToken cancellationToken = default) {
            IReadOnlyList<AgentDecisionPolicyPendingSummary> rows = Record is null ? [] : [new(Record.Command.Expectation.AgentId, Record.Command.OperationId, Record.Stage)];
            AfterList?.Invoke();
            return ValueTask.FromResult(rows);
        }
        public ValueTask<AgentDecisionPolicyPendingRecord?> ReadAsync(AgentDecisionPolicyPendingAddress context, CancellationToken cancellationToken = default) {
            Assert.Equal(Record?.Command.Expectation.AgentId, context.AgentId);
            AfterRead?.Invoke();
            return ValueTask.FromResult(Record);
        }
        public ValueTask<AgentDecisionPolicyPendingChange> PrepareAsync(ReplaceAgentDecisionDataPolicyCommand command, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Recovery cannot prepare a new save.");
        public ValueTask<AgentDecisionPolicyPendingChange> BeginDispatchAsync(ReplaceAgentDecisionDataPolicyCommand command, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Recovery cannot dispatch a save.");
        public ValueTask<AgentDecisionPolicyPendingChange> RecordOutcomeAsync(ReplaceAgentDecisionDataPolicyCommand command, AgentDecisionDataPolicySaveOutcome outcome, CancellationToken cancellationToken = default) {
            Assert.Equal(Record!.Command, command);
            Record = new(command, AgentDecisionPolicyPendingStage.Accepted, outcome);
            return ValueTask.FromResult(new AgentDecisionPolicyPendingChange(true, Record));
        }
        public ValueTask<AgentDecisionPolicyPendingChange> ClearResolvedAsync(ReplaceAgentDecisionDataPolicyCommand command, CancellationToken cancellationToken = default) {
            Assert.Equal(Record!.Command, command);
            Record = null;
            return ValueTask.FromResult(new AgentDecisionPolicyPendingChange(true, null));
        }
        public ValueTask<AgentDecisionPolicyPendingChange> CancelPreparedAsync(ReplaceAgentDecisionDataPolicyCommand command, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("This submission has been sent.");
    }
}
