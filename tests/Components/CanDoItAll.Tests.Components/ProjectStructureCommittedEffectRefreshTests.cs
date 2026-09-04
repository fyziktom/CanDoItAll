using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructureCommittedEffectRefreshTests
{
    [Fact]
    public async Task Successful_run_publishes_project_refresh()
    {
        var fixture = CreateFixture();
        var run = CreateRun(fixture, ExecutionState.Completed, RunOutcome.Succeeded);
        var notification = Assert.IsType<AgentChatExecutionCompleted>(
            AgentChatContextInvocationFactory.CreateCompletionNotification(run));

        await fixture.Hub.PublishAsync(notification);

        Assert.Equal(1, fixture.NotificationCount);
    }

    [Fact]
    public async Task Committed_effect_publishes_refresh_when_run_finishes_failed()
    {
        var fixture = CreateFixture();
        var run = CreateRun(fixture, ExecutionState.Failed, RunOutcome.Cancelled);
        var trace = CreateTrace(fixture.Source, AgentToolEffectState.Committed);
        var notification = Assert.IsType<AgentChatExecutionCompleted>(
            AgentChatContextInvocationFactory.CreateCompletionNotification(run, [trace]));

        await fixture.Hub.PublishAsync(notification);

        Assert.Equal(1, fixture.NotificationCount);
    }

    [Fact]
    public void Failed_run_without_committed_effect_does_not_publish_refresh()
    {
        var fixture = CreateFixture();
        var run = CreateRun(fixture, ExecutionState.Failed, RunOutcome.Failed);
        var trace = CreateTrace(fixture.Source, AgentToolEffectState.NotCommitted);

        Assert.Null(AgentChatContextInvocationFactory.CreateCompletionNotification(run, [trace]));
        Assert.Equal(0, fixture.NotificationCount);
    }

    [Fact]
    public void Committed_effect_for_another_project_does_not_publish_refresh()
    {
        var fixture = CreateFixture();
        var run = CreateRun(fixture, ExecutionState.Failed, RunOutcome.Failed);
        var unrelated = new AgentChatContextSource(
            fixture.Source.Kind,
            new AgentChatContextSourceId(Guid.NewGuid().ToString("D")));
        var trace = CreateTrace(unrelated, AgentToolEffectState.Committed);

        Assert.Null(AgentChatContextInvocationFactory.CreateCompletionNotification(run, [trace]));
        Assert.Equal(0, fixture.NotificationCount);
    }

    [Fact]
    public async Task Duplicate_execution_is_delivered_once_and_disposed_subscription_is_not_called()
    {
        var fixture = CreateFixture();
        var run = CreateRun(fixture, ExecutionState.Completed, RunOutcome.Succeeded);
        var notification = Assert.IsType<AgentChatExecutionCompleted>(
            AgentChatContextInvocationFactory.CreateCompletionNotification(run));

        await fixture.Hub.PublishAsync(notification);
        await fixture.Hub.PublishAsync(notification);
        fixture.Subscription.Dispose();
        await fixture.Hub.PublishAsync(new AgentChatExecutionCompleted(
            notification.ScopeId,
            notification.Source,
            notification.AgentId,
            notification.ChatSessionId,
            Guid.NewGuid(),
            notification.CompletedAtUtc));

        Assert.Equal(1, fixture.NotificationCount);
    }

    private static RefreshFixture CreateFixture()
    {
        var source = new AgentChatContextSource(
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId(Guid.NewGuid().ToString("D")));
        var hub = new AgentChatExecutionNotificationHub(
            NullLogger<AgentChatExecutionNotificationHub>.Instance);
        var fixture = new RefreshFixture(source, hub);
        fixture.Subscription = hub.Subscribe(source, _ =>
        {
            fixture.NotificationCount++;
            return Task.CompletedTask;
        });
        return fixture;
    }

    private static ExecutionRunRecord CreateRun(
        RefreshFixture fixture,
        ExecutionState state,
        RunOutcome outcome)
    {
        var completedAtUtc = new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var context = new AgentChatContextSnapshot(
            new AgentChatContextScope(
                AgentChatContextScopeId.Create(),
                fixture.Source,
                "Project structure",
                agentAccess:
                [
                    new AgentChatContextAgentAccess(
                        agentId,
                        AgentChatContextPermission.Read | AgentChatContextPermission.Mutate,
                        "Project structure")
                ],
                accessMode: AgentChatContextScopeAccessMode.AllowListed,
                completionRefreshMode: AgentChatContextCompletionRefreshMode.OnSuccessfulRun),
            [
                new AgentChatContextFragment(
                    new AgentChatContextContributorId("surface"),
                    0,
                    "Current project structure")
            ],
            Version: 1,
            CapturedAtUtc: completedAtUtc.AddMinutes(-1));
        var invocation = AgentChatContextInvocationFactory.Create(
            context,
            agentId,
            sessionId,
            "Create the project asset",
            AgentExecutionOperationId.New(),
            new DatabaseProfileGeneration(0),
            completedAtUtc);
        var invocationContext = Assert.IsType<ExecutionInvocationContext>(invocation.Options.Context);

        return new ExecutionRunRecord(
            Guid.NewGuid(),
            agentId,
            sessionId,
            "Create project asset",
            invocationContext.SourceKind,
            invocationContext.SourceId,
            invocationContext.CorrelationId,
            invocationContext.CausationId,
            invocationContext.RequestedBy,
            invocationContext.RequestedByKind,
            invocationContext.MetadataJson,
            "Create the project asset",
            "Tool execution did not complete cleanly.",
            "test-provider",
            "test-model",
            state,
            outcome,
            completedAtUtc.AddMinutes(-1),
            completedAtUtc,
            completedAtUtc.AddMinutes(-1),
            completedAtUtc,
            string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }

    private static AgentToolInvocationTrace CreateTrace(
        AgentChatContextSource source,
        AgentToolEffectState effectState)
    {
        var now = new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
        return new AgentToolInvocationTrace(
            "project_structure_asset_create",
            ToolInvocationClassification.Mutation,
            1,
            now,
            now,
            Succeeded: effectState == AgentToolEffectState.Committed,
            FailureMessage: effectState == AgentToolEffectState.Committed
                ? string.Empty
                : "The mutation did not commit.")
        {
            Outcome = effectState == AgentToolEffectState.Committed
                ? AgentToolInvocationOutcome.Succeeded
                : AgentToolInvocationOutcome.Failed,
            EffectState = effectState,
            EffectSourceKind = source.Kind.Value,
            EffectSourceId = source.Id.Value,
            OperationCorrelationKey = "asset-operation"
        };
    }

    private sealed class RefreshFixture(
        AgentChatContextSource source,
        AgentChatExecutionNotificationHub hub)
    {
        public AgentChatContextSource Source { get; } = source;

        public AgentChatExecutionNotificationHub Hub { get; } = hub;

        public IAgentChatExecutionNotificationSubscription Subscription { get; set; } = null!;

        public int NotificationCount { get; set; }
    }
}
