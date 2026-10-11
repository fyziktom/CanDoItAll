using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed partial class AgentFrameworkExecutionRunTrackingIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Approval_decision_applies_the_selected_run_policy_to_the_next_tool(bool approveRemaining) {
        var nextApproval = new PendingToolApprovalRecord(
            "approval-002", "call-002", "workspace_write_file", "function",
            "Write artifacts/summary.md.", """{"path":"artifacts/summary.md"}""");
        var runtime = new StructuredOutputApprovalRuntime {
            ContinuationResponses = [new AgentRuntimeResponse(
                ResponseText: "The next write requires approval.",
                InputTokens: 1, OutputTokens: 1, ToolCalls: 1,
                RuntimeSessionKey: "runtime-session-key-2", SerializedSessionStateJson: "{}",
                PendingApprovals: [nextApproval])]
        };
        await using var environment = CanDoItAllTestEnvironment.Create("integration-agentframework-approval-policy");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            environment.CreatePostgreSqlProfile("primary"), "CanDoItAll.Tests", TestSchemaBootstrapModules.Full,
            configureServices: services => {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton<IFakeAgentRuntime>(runtime);
                UseDirectWorkspaceService(services);
            });
        await using var scope = provider.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var store = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var agent = (await workspace.ListAgentsAsync(includeTemplates: false))
            .First(item => item.ProviderProfileId.HasValue && !item.Permissions.AutoApproveExternalCallsByDefault);
        var session = await workspace.GetOrCreateChatSessionAsync(agent.Id);
        var initial = await workspace.SendMessageAsync(agent.Id, session.Id, "Write the result and then its summary.",
            options: new AgentChatRunOptions(AgentExecutionOperationId.New()));
        var pending = await store.GetExecutionRunDetailAsync(initial.ExecutionRunId);
        Assert.NotNull(pending);
        var firstApproval = Assert.Single(pending.Run.PendingApprovals);

        var continued = await workspace.RespondToPendingApprovalsAsync(agent.Id, session.Id,
            AgentExecutionOperationId.New(), [new(firstApproval.ApprovalId, Approved: true)],
            autoApprovePendingToolCalls: approveRemaining);

        var saved = await store.GetExecutionRunDetailAsync(continued.ExecutionRunId);
        Assert.NotNull(saved);
        Assert.Equal(initial.ExecutionRunId, continued.ExecutionRunId);
        Assert.Equal(approveRemaining, saved.Run.AutoApprovePendingToolCalls);
        Assert.Equal(approveRemaining ? ExecutionState.Completed : ExecutionState.WaitingOnTool, saved.Run.State);
        Assert.Equal(approveRemaining ? 2 : 1, runtime.ContinuationSessionPendingApprovals.Count);
        Assert.Equal(firstApproval.ApprovalId, Assert.Single(runtime.ContinuationSessionPendingApprovals[0]).ApprovalId);
        Assert.All(runtime.ContinuationSuppressApprovalRequirements, Assert.False);
        if (approveRemaining) {
            Assert.Empty(saved.Run.PendingApprovals);
            Assert.Equal(nextApproval.ApprovalId, Assert.Single(runtime.ContinuationSessionPendingApprovals[1]).ApprovalId);
        } else {
            Assert.Equal(nextApproval.ApprovalId, Assert.Single(saved.Run.PendingApprovals).ApprovalId);
        }
    }
}
