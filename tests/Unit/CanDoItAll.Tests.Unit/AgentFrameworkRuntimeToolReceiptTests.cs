using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentFrameworkRuntimeToolReceiptTests
{
    [Fact]
    public void CreateToolInvocationTraceReceipts_preserves_runtime_provider_receipts()
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
                    Signature = "project_structure_node_create|projectId=<redacted>,request={...}",
                    DirectReceiptExecutionRunId = runId
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
                    Signature = "project_structure_read|projectId=<redacted>",
                    DirectReceiptExecutionRunId = runId
                }
            ]
        };

        var receipts = AgentFrameworkWorkspaceExecutionService.CreateToolInvocationTraceReceipts(run, response);

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

    [Fact]
    public void CreateToolInvocationTraceReceipts_projects_completed_unreceipted_tool_traces()
    {
        var runId = Guid.NewGuid();
        var startedAtUtc = DateTimeOffset.UtcNow;
        var run = CreateRun(runId, startedAtUtc);
        var response = new AgentRuntimeResponse(
            "{}",
            InputTokens: 0,
            OutputTokens: 0,
            ToolCalls: 2,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [])
        {
            ToolInvocationTraces =
            [
                new AgentToolInvocationTrace(
                    "workspace_list_files",
                    ToolInvocationClassification.Read,
                    Sequence: 1,
                    StartedAtUtc: startedAtUtc.AddSeconds(1),
                    CompletedAtUtc: startedAtUtc.AddSeconds(2),
                    Succeeded: false,
                    FailureMessage: "External workspace root is not authorized for this run.")
                {
                    Signature = "workspace_list_files|root=<redacted>,searchPattern=**/*.csproj"
                },
                new AgentToolInvocationTrace(
                    "load_skill",
                    ToolInvocationClassification.Read,
                    Sequence: 2,
                    StartedAtUtc: startedAtUtc.AddSeconds(3),
                    CompletedAtUtc: startedAtUtc.AddSeconds(4),
                    Succeeded: true,
                    FailureMessage: string.Empty)
                {
                    Signature = "load_skill|name=project-structure"
                }
            ]
        };

        var receipts = AgentFrameworkWorkspaceExecutionService.CreateToolInvocationTraceReceipts(run, response);

        Assert.Equal(2, receipts.Count);
        Assert.All(receipts, receipt =>
        {
            Assert.Equal(runId, receipt.ExecutionRunId);
            Assert.Equal("agent-tool-trace", receipt.ToolFamily);
            Assert.Equal("PolicyEnforced", receipt.ApprovalMode);
            Assert.Equal("ToolInvocationPolicy", receipt.IsolationGuarantee);
            Assert.Empty(receipt.RuntimeToolProviderKey);
        });

        var failedReceipt = receipts.Single(receipt => receipt.ToolName == "workspace_list_files");
        Assert.Equal("ToolInvocation:Read", failedReceipt.RiskClass);
        Assert.Contains("External workspace root", failedReceipt.ExitSummary, StringComparison.Ordinal);

        var successfulReceipt = receipts.Single(receipt => receipt.ToolName == "load_skill");
        Assert.Equal("Succeeded", successfulReceipt.ExitSummary);

        var repeatedReceipts = AgentFrameworkWorkspaceExecutionService.CreateToolInvocationTraceReceipts(run, response);
        Assert.Equal(
            receipts.Select(receipt => receipt.Id).Order().ToArray(),
            repeatedReceipts.Select(receipt => receipt.Id).Order().ToArray());
    }

    [Fact]
    public void CreateToolInvocationTraceReceipts_skips_trace_with_same_run_direct_receipt()
    {
        var runId = Guid.NewGuid();
        var startedAtUtc = DateTimeOffset.UtcNow;
        var run = CreateRun(runId, startedAtUtc);
        var response = CreateResponse(
            new AgentToolInvocationTrace(
                "workspace_read_file",
                ToolInvocationClassification.Read,
                Sequence: 1,
                StartedAtUtc: startedAtUtc.AddSeconds(1),
                CompletedAtUtc: startedAtUtc.AddSeconds(2),
                Succeeded: true,
                FailureMessage: string.Empty)
            {
                DirectReceiptExecutionRunId = runId
            });

        var receipts = AgentFrameworkWorkspaceExecutionService.CreateToolInvocationTraceReceipts(run, response);

        Assert.Empty(receipts);
    }

    [Fact]
    public void CreateToolInvocationTraceReceipts_does_not_trust_direct_receipt_from_another_run()
    {
        var runId = Guid.NewGuid();
        var startedAtUtc = DateTimeOffset.UtcNow;
        var run = CreateRun(runId, startedAtUtc);
        var response = CreateResponse(
            new AgentToolInvocationTrace(
                "workspace_read_file",
                ToolInvocationClassification.Read,
                Sequence: 1,
                StartedAtUtc: startedAtUtc.AddSeconds(1),
                CompletedAtUtc: startedAtUtc.AddSeconds(2),
                Succeeded: true,
                FailureMessage: string.Empty)
            {
                DirectReceiptExecutionRunId = Guid.NewGuid()
            });

        var receipt = Assert.Single(
            AgentFrameworkWorkspaceExecutionService.CreateToolInvocationTraceReceipts(run, response));

        Assert.Equal("agent-tool-trace", receipt.ToolFamily);
        Assert.Equal(runId, receipt.ExecutionRunId);
    }

    [Fact]
    public void CreateToolInvocationTraceReceipts_skips_incomplete_and_unclassified_traces()
    {
        var runId = Guid.NewGuid();
        var startedAtUtc = DateTimeOffset.UtcNow;
        var run = CreateRun(runId, startedAtUtc);
        var response = CreateResponse(
            new AgentToolInvocationTrace(
                "workspace_read_file",
                ToolInvocationClassification.Read,
                Sequence: 1,
                StartedAtUtc: startedAtUtc.AddSeconds(1),
                CompletedAtUtc: null,
                Succeeded: false,
                FailureMessage: string.Empty),
            new AgentToolInvocationTrace(
                "unknown_tool",
                ToolInvocationClassification.Unknown,
                Sequence: 2,
                StartedAtUtc: startedAtUtc.AddSeconds(2),
                CompletedAtUtc: startedAtUtc.AddSeconds(3),
                Succeeded: true,
                FailureMessage: string.Empty));

        var receipts = AgentFrameworkWorkspaceExecutionService.CreateToolInvocationTraceReceipts(run, response);

        Assert.Empty(receipts);
    }

    [Fact]
    public void CreateToolInvocationTraceReceipts_distinguishes_repeated_invocations_across_runtime_turns()
    {
        var runId = Guid.NewGuid();
        var startedAtUtc = DateTimeOffset.UtcNow;
        var run = CreateRun(runId, startedAtUtc);
        var firstResponse = CreateResponse(CreateRepeatedTrace(startedAtUtc.AddSeconds(1)));
        var continuationResponse = CreateResponse(CreateRepeatedTrace(startedAtUtc.AddSeconds(3)));

        var firstReceipt = Assert.Single(
            AgentFrameworkWorkspaceExecutionService.CreateToolInvocationTraceReceipts(run, firstResponse));
        var continuationReceipt = Assert.Single(
            AgentFrameworkWorkspaceExecutionService.CreateToolInvocationTraceReceipts(run, continuationResponse));

        Assert.NotEqual(firstReceipt.Id, continuationReceipt.Id);
        Assert.Equal(firstReceipt.ToolName, continuationReceipt.ToolName);
        Assert.Equal(firstReceipt.RequestSummary, continuationReceipt.RequestSummary);

        AgentToolInvocationTrace CreateRepeatedTrace(DateTimeOffset traceStartedAtUtc)
        {
            return new AgentToolInvocationTrace(
                "workspace_list_files",
                ToolInvocationClassification.Read,
                Sequence: 1,
                StartedAtUtc: traceStartedAtUtc,
                CompletedAtUtc: traceStartedAtUtc.AddSeconds(1),
                Succeeded: true,
                FailureMessage: string.Empty)
            {
                Signature = "workspace_list_files|root=<redacted>,searchPattern=**/*.csproj"
            };
        }
    }

    [Fact]
    public void AggregateToolInvocationTraces_preserves_prior_turns_and_rebases_continuation_sequences()
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var initialTraces = new[]
        {
            CreateTrace("workspace_list_files", sequence: 1, startedAtUtc.AddSeconds(1)),
            CreateTrace("workspace_read_file", sequence: 2, startedAtUtc.AddSeconds(2))
        };
        var continuationTraces = new[]
        {
            CreateTrace("workspace_write_file", sequence: 1, startedAtUtc.AddSeconds(3))
        };

        var traces = AgentFrameworkWorkspaceExecutionService.AggregateToolInvocationTraces(
            initialTraces,
            continuationTraces);

        Assert.Equal(
            ["workspace_list_files", "workspace_read_file", "workspace_write_file"],
            traces.Select(trace => trace.ToolName).ToArray());
        Assert.Equal([1, 2, 3], traces.Select(trace => trace.Sequence).ToArray());

        static AgentToolInvocationTrace CreateTrace(
            string toolName,
            int sequence,
            DateTimeOffset traceStartedAtUtc)
        {
            return new AgentToolInvocationTrace(
                toolName,
                ToolInvocationClassification.Read,
                sequence,
                traceStartedAtUtc,
                traceStartedAtUtc.AddMilliseconds(100),
                Succeeded: true,
                FailureMessage: string.Empty);
        }
    }

    [Fact]
    public void ResolveFailureToolInvocationTraces_preserves_prior_continuation_and_provider_failure_traces()
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var priorTrace = CreateFailureTrace(
            "workspace_list_files",
            startedAtUtc);
        var failureTrace = CreateFailureTrace(
            "workspace_read_file",
            startedAtUtc.AddSeconds(1));
        var lastResponse = CreateResponse(priorTrace);
        var exception = new AgentRuntimeUsageException(
            "Provider failed after its tool completed.",
            new InvalidOperationException("provider failure"),
            [],
            [failureTrace]);

        var traces = AgentFrameworkWorkspaceExecutionService.ResolveFailureToolInvocationTraces(
            lastResponse,
            exception);

        Assert.Equal(
            ["workspace_list_files", "workspace_read_file"],
            traces.Select(trace => trace.ToolName).ToArray());
        Assert.Equal([1, 2], traces.Select(trace => trace.Sequence).ToArray());

        static AgentToolInvocationTrace CreateFailureTrace(
            string toolName,
            DateTimeOffset traceStartedAtUtc)
            => new(
                toolName,
                ToolInvocationClassification.Read,
                Sequence: 1,
                StartedAtUtc: traceStartedAtUtc,
                CompletedAtUtc: traceStartedAtUtc.AddMilliseconds(100),
                Succeeded: true,
                FailureMessage: string.Empty);
    }

    private static AgentRuntimeResponse CreateResponse(params AgentToolInvocationTrace[] traces)
    {
        return new AgentRuntimeResponse(
            "{}",
            InputTokens: 0,
            OutputTokens: 0,
            ToolCalls: traces.Length,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [])
        {
            ToolInvocationTraces = traces
        };
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
