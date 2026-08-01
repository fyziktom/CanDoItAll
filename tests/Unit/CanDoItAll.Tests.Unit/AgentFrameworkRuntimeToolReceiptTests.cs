using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentFrameworkRuntimeToolReceiptTests
{
    [Fact]
    public void CreateRuntimeProviderToolReceipts_projects_successful_runtime_provider_traces()
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

        Assert.Equal(2, receipts.Count);

        var createReceipt = receipts.Single(receipt => receipt.ToolName == "project_structure_node_create");
        Assert.Equal(runId, createReceipt.ExecutionRunId);
        Assert.Equal("runtime-provider", createReceipt.ToolFamily);
        Assert.Equal("RuntimeProvider:Mutation", createReceipt.RiskClass);
        Assert.Equal("PolicyEnforced", createReceipt.ApprovalMode);
        Assert.Equal("RuntimeProviderPolicy", createReceipt.IsolationGuarantee);
        Assert.Equal("Succeeded", createReceipt.ExitSummary);
        Assert.Equal("project-structure.runtime-tools", createReceipt.RuntimeToolProviderKey);
        Assert.Equal("Project structure runtime tools", createReceipt.RuntimeToolProviderName);
        Assert.Contains("project_structure_node_create", createReceipt.RequestSummary, StringComparison.Ordinal);

        var readReceipt = receipts.Single(receipt => receipt.ToolName == "project_structure_read");
        Assert.Equal(runId, readReceipt.ExecutionRunId);
        Assert.Equal("runtime-provider", readReceipt.ToolFamily);
        Assert.Equal("RuntimeProvider:Read", readReceipt.RiskClass);
        Assert.Equal("PolicyEnforced", readReceipt.ApprovalMode);
        Assert.Equal("RuntimeProviderPolicy", readReceipt.IsolationGuarantee);
        Assert.Equal("Succeeded", readReceipt.ExitSummary);
        Assert.Equal("project-structure.runtime-tools", readReceipt.RuntimeToolProviderKey);
        Assert.Equal("Project structure runtime tools", readReceipt.RuntimeToolProviderName);
        Assert.Contains("project_structure_read", readReceipt.RequestSummary, StringComparison.Ordinal);
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
