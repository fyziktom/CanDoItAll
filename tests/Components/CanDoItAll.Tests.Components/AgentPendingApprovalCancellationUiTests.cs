using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Web;
using static CanDoItAll.Tests.Components.AgentFramework.AgentChatPanelResponsivenessTests;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentPendingApprovalCancellationUiTests {
    [Theory]
    [InlineData(AgentApprovalCheckpointFailure.Missing)]
    [InlineData(AgentApprovalCheckpointFailure.Corrupt)]
    [InlineData(AgentApprovalCheckpointFailure.Incompatible)]
    [InlineData(AgentApprovalCheckpointFailure.Unavailable)]
    public async Task Unavailable_checkpoint_preserves_pending_intent_and_displays_safe_reconciliation(AgentApprovalCheckpointFailure failure) {
        var fixture = Create();
        using var context = fixture.Context;
        var effects = (AgentChatEffectOwnershipTests.EffectsProxy)(object)context.Services.GetRequiredService<IAgentChatExecutionOrchestrator>();
        var cut = Render(context, fixture.Agent, fixture.Session);
        await cut.WaitForElement("[data-testid='chat-approve-once-button']").ClickAsync(new MouseEventArgs());
        effects.Approval.SetException(new AgentApprovalCheckpointUnavailableException(failure, new IOException("PRIVATE_CHECKPOINT_PATH")));
        cut.WaitForAssertion(() => {
            var message = Assert.Single(context.Services.GetRequiredService<NotificationService>().Messages);
            Assert.Equal("Attention", message.Summary);
            Assert.Contains("No approval was applied", message.Detail, StringComparison.Ordinal);
            Assert.Contains("cancel the pending run", message.Detail, StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE_CHECKPOINT_PATH", message.Detail, StringComparison.Ordinal);
            Assert.False(cut.FindComponent<ChatWorkspacePanel>().Instance.IsBusy);
            Assert.Equal(fixture.Run.PendingApprovals, cut.FindComponent<ChatWorkspacePanel>().Instance.ActiveRun!.PendingApprovals);
            Assert.False(CancelButton(cut).Disabled);
        });
        Assert.Empty(fixture.Commands);
    }

    [Fact]
    public async Task Duplicate_cancel_is_one_command_and_blocks_contradictory_approval_actions() {
        var fixture = Create();
        using var context = fixture.Context;
        var cut = Render(context, fixture.Agent, fixture.Session);
        var click = CancelButton(cut).Click;
        var pending = cut.InvokeAsync(() => click.InvokeAsync());
        cut.WaitForAssertion(() => Assert.Single(fixture.Commands));
        Assert.True(cut.FindComponent<ChatWorkspacePanel>().Instance.IsBusy);
        Assert.True(CancelButton(cut).Disabled);
        await cut.InvokeAsync(() => click.InvokeAsync());
        Assert.Single(fixture.Commands);
        var cancelled = Cancelled(fixture.Run, fixture.Session);
        fixture.Reads.Workspace = (_, _, _) => Task.FromResult(CreateWorkspace(fixture.Agent.Id, fixture.Session, cancelled.Run));
        fixture.Reads.Detail = (_, _) => Task.FromResult(cancelled);
        fixture.Completion.SetResult(cancelled);
        await pending;
        Assert.Empty(cut.FindAll("[data-testid='agents-chat-cancel-pending-run']"));
        Assert.Contains(context.Services.GetRequiredService<NotificationService>().Messages, item => item.Detail.Contains("pending run was cancelled", StringComparison.Ordinal));
        Assert.False(cut.FindComponent<ChatWorkspacePanel>().Instance.IsBusy);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task A_retired_view_does_not_cancel_the_command_or_publish_its_result(bool dispose, bool failure) {
        var fixture = Create();
        using var context = fixture.Context;
        var cut = Render(context, fixture.Agent, fixture.Session);
        var click = CancelButton(cut).Click;
        var pending = cut.InvokeAsync(() => click.InvokeAsync());
        cut.WaitForAssertion(() => Assert.Single(fixture.Commands));
        var next = CreateSession(fixture.Agent.Id) with { Title = "Another conversation" };
        if (dispose) {
            await context.DisposeRenderedComponentsAsync();
        } else {
            fixture.Reads.Workspace = (_, id, _) => Task.FromResult(CreateWorkspace(fixture.Agent.Id, id == next.Id ? next : fixture.Session));
            await cut.InvokeAsync(() => cut.Render(p => p.Add(x => x.PreferredAgentId, fixture.Agent.Id).Add(x => x.PreferredSessionId, next.Id)));
        }
        Assert.False(fixture.Commands[0].CancellationToken.IsCancellationRequested);
        if (failure) {
            fixture.Completion.SetException(new IOException("PRIVATE_PROVIDER_DETAIL"));
        } else {
            fixture.Completion.SetResult(Cancelled(fixture.Run, fixture.Session));
        }
        await pending;
        Assert.DoesNotContain(context.Services.GetRequiredService<NotificationService>().Messages,
            item => item.Detail.Contains("pending run was cancelled", StringComparison.Ordinal) || item.Detail.Contains("PRIVATE_PROVIDER_DETAIL", StringComparison.Ordinal));
        if (!dispose) {
            Assert.Equal(next.Id, cut.FindComponent<ChatWorkspacePanel>().Instance.Session!.Id);
            Assert.False(cut.FindComponent<ChatWorkspacePanel>().Instance.IsBusy);
        }
    }

    [Fact]
    public async Task Known_cancellation_remains_known_when_refresh_fails() {
        var fixture = Create();
        using var context = fixture.Context;
        var cut = Render(context, fixture.Agent, fixture.Session);
        var pending = cut.InvokeAsync(() => CancelButton(cut).Click.InvokeAsync());
        cut.WaitForAssertion(() => Assert.Single(fixture.Commands));
        fixture.Reads.Workspace = (_, _, _) => throw new IOException("PRIVATE_READ_FAILURE");
        fixture.Completion.SetResult(Cancelled(fixture.Run, fixture.Session));
        await pending;
        var messages = context.Services.GetRequiredService<NotificationService>().Messages;
        Assert.Contains(messages, item => item.Detail.Contains("run was cancelled", StringComparison.Ordinal));
        Assert.DoesNotContain(messages, item => item.Detail.Contains("PRIVATE_READ_FAILURE", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_receipt_for_another_run_cannot_report_success() {
        var fixture = Create();
        using var context = fixture.Context;
        var cut = Render(context, fixture.Agent, fixture.Session);
        var pending = cut.InvokeAsync(() => CancelButton(cut).Click.InvokeAsync());
        cut.WaitForAssertion(() => Assert.Single(fixture.Commands));
        fixture.Completion.SetResult(Cancelled(fixture.Run with { Id = Guid.NewGuid() }, fixture.Session));
        await pending;
        var messages = context.Services.GetRequiredService<NotificationService>().Messages;
        Assert.Contains(messages, item => item.Detail.Contains("could not be confirmed", StringComparison.Ordinal));
        Assert.DoesNotContain(messages, item => item.Detail.Contains("pending run was cancelled", StringComparison.Ordinal));
    }

    private static Fixture Create() {
        var (service, reads) = AgentChatSessionTests.CreateReads();
        var agent = CreateAgent();
        var session = CreateSession(agent.Id);
        var run = CreateRunningRun(agent.Id, session.Id) with {
            State = ExecutionState.WaitingOnTool,
            PendingApprovals = [new("pending-ui", "call-ui", ToolContractCatalog.WorkspaceWriteFile, "function", "Write approved file", "{}")]
        };
        reads.Agents = [agent];
        reads.Workspace = (_, _, _) => Task.FromResult(CreateWorkspace(agent.Id, session, run));
        reads.Detail = (_, _) => Task.FromResult(new ExecutionRunDetail(run, session, [], []));
        var completion = new TaskCompletionSource<ExecutionRunDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commands = new List<Command>();
        reads.CancelPending = (id, token) => {
            commands.Add(new(id, token));
            return completion.Task;
        };
        var context = CreateContext(service, DispatchProxy.Create<IAgentChatExecutionOrchestrator, AgentChatEffectOwnershipTests.EffectsProxy>());
        return new(context, reads, agent, session, run, completion, commands);
    }

    private static IRenderedComponent<AgentChatPanel> Render(BunitContext context, AgentDefinition agent, ChatSessionRecord session)
        => context.Render<AgentChatPanel>(p => p.Add(x => x.PreferredAgentId, agent.Id).Add(x => x.PreferredSessionId, session.Id));

    private static Button CancelButton(IRenderedComponent<AgentChatPanel> cut)
        => cut.FindComponents<Button>().Single(item => item.Instance.Text == "Cancel pending run").Instance;

    private static ExecutionRunDetail Cancelled(ExecutionRunRecord run, ChatSessionRecord session)
        => new(run with { State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled, PendingApprovals = [], CompletedAtUtc = DateTimeOffset.UtcNow }, session, [], []);

    private sealed record Command(Guid RunId, CancellationToken CancellationToken);
    private sealed record Fixture(BunitContext Context, AgentChatSessionTests.ReadProxy Reads, AgentDefinition Agent,
        ChatSessionRecord Session, ExecutionRunRecord Run, TaskCompletionSource<ExecutionRunDetail> Completion, List<Command> Commands);
}
