using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentFrameworkRuntimeToolReceiptTests
{
    [Fact]
    public void CreateRuntimeProviderToolReceipts_projects_successful_mutation_traces()
    {
        var runId = Guid.NewGuid();
        var startedAtUtc = DateTimeOffset.UtcNow;
        var run = CreateRun(runId, startedAtUtc);
        var response = new AgentRuntimeResponse(
            "{}",
            InputTokens: 0,
            OutputTokens: 0,
            ToolCalls: 1,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [])
        {
            ToolInvocationTraces =
            [
                new AgentToolInvocationTrace(
                    "project_structure_node_create",
                    ToolInvocationClassification.Mutation,
                    Sequence: 1,
                    StartedAtUtc: startedAtUtc.AddSeconds(1),
                    CompletedAtUtc: startedAtUtc.AddSeconds(2),
                    Succeeded: true,
                    FailureMessage: string.Empty)
                {
                    RuntimeToolProviderKey = "project-structure.runtime-tools",
                    RuntimeToolProviderName = "Project structure runtime tools",
                    Signature = "project_structure_node_create|projectId=<redacted>,request={...}"
                },
                new AgentToolInvocationTrace(
                    "project_structure_read",
                    ToolInvocationClassification.Read,
                    Sequence: 2,
                    StartedAtUtc: startedAtUtc.AddSeconds(3),
                    CompletedAtUtc: startedAtUtc.AddSeconds(4),
                    Succeeded: true,
                    FailureMessage: string.Empty)
                {
                    RuntimeToolProviderKey = "project-structure.runtime-tools",
                    RuntimeToolProviderName = "Project structure runtime tools",
                    Signature = "project_structure_read|projectId=<redacted>"
                }
            ]
        };

        var receipts = AgentFrameworkWorkspaceExecutionService.CreateRuntimeProviderToolReceipts(run, response);

        var receipt = Assert.Single(receipts);
        Assert.Equal(runId, receipt.ExecutionRunId);
        Assert.Equal("runtime-provider", receipt.ToolFamily);
        Assert.Equal("project_structure_node_create", receipt.ToolName);
        Assert.Equal("RuntimeProvider:Mutation", receipt.RiskClass);
        Assert.Equal("PolicyEnforced", receipt.ApprovalMode);
        Assert.Equal("RuntimeProviderPolicy", receipt.IsolationGuarantee);
        Assert.Equal("Succeeded", receipt.ExitSummary);
        Assert.Equal("project-structure.runtime-tools", receipt.RuntimeToolProviderKey);
        Assert.Equal("Project structure runtime tools", receipt.RuntimeToolProviderName);
        Assert.Contains("project_structure_node_create", receipt.RequestSummary, StringComparison.Ordinal);
    }

    private static ExecutionRunRecord CreateRun(Guid runId, DateTimeOffset now)
    {
        return new ExecutionRunRecord(
            Id: runId,
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Runtime tool receipt projection",
            SourceKind: "process-step",
            SourceId: "step-1",
            CorrelationId: "corr-1",
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "system",
            MetadataJson: "{}",
            InputSummary: "Test runtime tool receipt projection.",
            ResultSummary: string.Empty,
            ProviderName: "Test provider",
            Model: "test-model",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [])
        {
            ProcessRunId = Guid.NewGuid().ToString("D"),
            ProcessStepId = Guid.NewGuid().ToString("D")
        };
    }
}
