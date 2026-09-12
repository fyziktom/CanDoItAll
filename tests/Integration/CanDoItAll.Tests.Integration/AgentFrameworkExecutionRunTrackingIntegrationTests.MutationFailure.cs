using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed partial class AgentFrameworkExecutionRunTrackingIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unresolved_required_mutation_is_persisted_as_tool_execution_failure_without_an_output_schema(bool approvalContinuation) {
        var trace = CreateToolInvocationTrace("workspace_write_file", ToolInvocationClassification.Mutation, 1) with {
            Succeeded = false, Outcome = AgentToolInvocationOutcome.Unknown, EffectState = AgentToolEffectState.Unknown,
            FailureCode = "UnverifiedToolResult", FailureMessage = "The mutation tool returned no trusted outcome evidence."
        };
        var runtime = new StructuredOutputApprovalRuntime {
            InitialResponseText = "Useful assistant prose.",
            ContinuationResponseText = "Useful assistant prose after approval.",
            InitialPendingApprovals = approvalContinuation
                ? [new("approval-001", "call-001", "workspace_write_file", "function", "Write the reviewed file.", "{\"path\":\"artifacts/result.md\"}")]
                : [],
            InitialToolInvocationTraces = approvalContinuation ? [] : [trace],
            ContinuationToolInvocationTraces = [trace]
        };
        await using var environment = CanDoItAllTestEnvironment.Create("integration-required-mutation-phase");
        var profile = environment.CreatePostgreSqlProfile("primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(profile, "CanDoItAll.Tests",
            TestSchemaBootstrapModules.Full, configureServices: services => {
                services.RemoveAll<IFakeAgentRuntime>();
                services.RouteRuntimePortsThroughAgentRuntime();
                services.AddSingleton<IFakeAgentRuntime>(runtime);
                UseDirectWorkspaceService(services);
            });
        await using var scope = provider.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = (await workspace.ListAgentsAsync(includeTemplates: false)).First(item => item.ProviderProfileId.HasValue);
        var chat = await workspace.GetOrCreateChatSessionAsync(agent.Id);
        var result = await workspace.ExecuteRunAsync(new(agent.Id, "Perform the reviewed mutation.", AgentExecutionOperationId.New(),
            chat.Id, AutoApprovePendingToolCalls: false));
        if (approvalContinuation) {
            var pending = (await workspace.GetExecutionRunDetailAsync(result.ExecutionRunId))!;
            Assert.Equal(ExecutionState.WaitingOnTool, pending.Run.State);
            result = await workspace.ContinueExecutionRunAsync(result.ExecutionRunId, AgentExecutionOperationId.New(),
                decisions: pending.Run.PendingApprovals.Select(item => new PendingToolApprovalDecision(item.ApprovalId, true)).ToArray(),
                autoApprovePendingToolCalls: false);
        }
        var saved = (await scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>()
            .GetExecutionRunDetailAsync(result.ExecutionRunId))!;
        Assert.Equal(ExecutionState.Failed, saved.Run.State);
        Assert.Equal(RunOutcome.Failed, saved.Run.Outcome);
        Assert.Contains("Required mutation", saved.Run.ResultSummary, StringComparison.Ordinal);
        Assert.Empty(saved.Run.StructuredOutputContractKey);
        Assert.Contains(saved.ExecutionLog, entry => entry.State == ExecutionState.Failed && entry.Phase == "Tool execution" &&
            entry.Message.Contains("unresolved required mutation", StringComparison.Ordinal));
        Assert.DoesNotContain(saved.ExecutionLog, entry => entry.State == ExecutionState.Failed && entry.Phase == "Output validation");
    }
}
