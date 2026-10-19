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

    private static BunitContext Context(out PolicyWorkspace proxy, out AgentDecisionPolicyEditorState state) {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, PolicyWorkspace>();
        proxy = (PolicyWorkspace)(object)workspace;
        state = new(proxy.AgentId);
        context.Services.AddSingleton(workspace);
        return context;
    }

    public class PolicyWorkspace : DispatchProxy {
        public Guid AgentId { get; } = Guid.NewGuid();
        public AgentExecutionActivityWorkspaceIdentity Workspace { get; } = new(Guid.NewGuid(), WorkspaceScopeDescriptor.Sandbox, new(1));
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<AgentDecisionDataPolicySaveOutcome> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ReplaceAgentDecisionDataPolicyCommand? Submitted { get; private set; }
        public int Writes { get; private set; }
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
            throw new InvalidOperationException("Unexpected policy editor dispatch.");
        }
    }
}
