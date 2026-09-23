using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class BrowserRuntimeLifecycleCompletionGateContributionTests
{
    [Fact]
    public void Validate_accepts_current_execution_browser_proof_for_the_started_host()
    {
        var executionRunId = Guid.NewGuid();
        var contribution = new BrowserRuntimeLifecycleCompletionGateContribution();

        var issue = contribution.Validate(CreateContext(
            executionRunId,
            runHost: "http://127.0.0.1:5173",
            browserHost: "http://127.0.0.1:5173"));

        Assert.Null(issue);
        Assert.Equal(ProcessCompletionGateContributionStage.BeforeToolReceiptEvidence, contribution.Stage);
    }

    [Fact]
    public void Validate_rejects_browser_proof_for_a_different_host()
    {
        var executionRunId = Guid.NewGuid();
        var contribution = new BrowserRuntimeLifecycleCompletionGateContribution();

        var issue = contribution.Validate(CreateContext(
            executionRunId,
            runHost: "http://127.0.0.1:5173",
            browserHost: "http://127.0.0.1:5174"));

        Assert.NotNull(issue);
        Assert.Equal("process.adapter.runtime_lifecycle_correlation_missing", issue.Code);
        Assert.Contains("not correlated", issue.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("runtime-provider")]
    [InlineData("agent-tool-trace")]
    public void Validate_uses_command_lifecycle_receipts_when_later_invocation_traces_exist(string traceFamily) {
        var executionRunId = Guid.NewGuid();
        var context = CreateContext(executionRunId, "http://127.0.0.1:5173", "http://127.0.0.1:5173");
        var receipts = context.ToolReceipts!;
        var traceCompletedAt = receipts.Max(receipt => receipt.CompletedAtUtc).AddSeconds(1);
        var contribution = new BrowserRuntimeLifecycleCompletionGateContribution();

        var issue = contribution.Validate(context with {
            ToolReceipts = [
                .. receipts,
                receipts[0] with {
                    ToolFamily = traceFamily,
                    RequestSummary = "workspace_sample_run|targetPath=src/App/App.csproj",
                    CompletedAtUtc = traceCompletedAt
                },
                receipts[3] with {
                    ToolFamily = traceFamily,
                    RequestSummary = "workspace_sample_stop|startupReceiptPath=artifacts/process-runs/test/tool-runs/runtime/startup.json",
                    CompletedAtUtc = traceCompletedAt
                }
            ]
        });

        Assert.Null(issue);
    }

    [Fact]
    public void Validate_rejects_invocation_traces_without_command_lifecycle_receipts() {
        var executionRunId = Guid.NewGuid();
        var context = CreateContext(executionRunId, "http://127.0.0.1:5173", "http://127.0.0.1:5173");
        var contribution = new BrowserRuntimeLifecycleCompletionGateContribution();

        var issue = contribution.Validate(context with {
            ToolReceipts = context.ToolReceipts!
                .Select(receipt => receipt with { ToolFamily = "runtime-provider" })
                .ToArray()
        });

        Assert.NotNull(issue);
        Assert.Contains("current execution-run host lifecycle", issue.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_rejects_latest_command_lifecycle_mismatch_even_when_earlier_pair_matches() {
        var executionRunId = Guid.NewGuid();
        var context = CreateContext(executionRunId, "http://127.0.0.1:5173", "http://127.0.0.1:5173");
        var receipts = context.ToolReceipts!;
        var contribution = new BrowserRuntimeLifecycleCompletionGateContribution();

        var issue = contribution.Validate(context with {
            ToolReceipts = [
                .. receipts,
                receipts[0] with {
                    RequestSummary = "startupReceipt=artifacts/process-runs/test/tool-runs/different/startup.json; hostUrl=http://127.0.0.1:5173",
                    CompletedAtUtc = receipts.Max(receipt => receipt.CompletedAtUtc).AddSeconds(1)
                }
            ]
        });

        Assert.NotNull(issue);
        Assert.Contains("same startup.json receipt", issue.Summary, StringComparison.Ordinal);
    }

    private static ProcessCompletionGateContext CreateContext(
        Guid executionRunId,
        string runHost,
        string browserHost)
    {
        var startupReceipt = "artifacts/process-runs/test/tool-runs/runtime/startup.json";
        var assignment = new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "runtime-proof",
            "qa",
            "qa",
            "QA",
            ProcessLaunchExecutorKinds.Agent,
            "agent",
            "Agent",
            "Capture runtime proof.",
            "sha256:readiness",
            "Test assignment.",
            [ArtifactSlotId.New()],
            [],
            [],
            ProcessOperationContractNames.ExternalProductTargetReadOnly,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] = JsonSerializer.Serialize(new[]
                {
                    "workspace_sample_run",
                    "browser_navigate",
                    "browser_snapshot",
                    "workspace_sample_stop"
                })
            },
            BranchGate: null,
            DateTimeOffset.UtcNow);
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            BranchOutcomeKey = "accepted",
            Reason = "Runtime proof accepted."
        };

        return new ProcessCompletionGateContext(
            assignment,
            output,
            [
                CreateReceipt("workspace_sample_run", $"startupReceipt={startupReceipt}; hostUrl={runHost}", executionRunId),
                CreateReceipt("browser_navigate", $"url={browserHost}", executionRunId),
                CreateReceipt("browser_snapshot", $"url={browserHost}", executionRunId),
                CreateReceipt("workspace_sample_stop", $"startupReceipt={startupReceipt}; hostUrl={runHost}", executionRunId)
            ],
            executionRunId);
    }

    private static ToolExecutionReceiptRecord CreateReceipt(
        string toolName,
        string requestSummary,
        Guid executionRunId)
        => new(
            Guid.NewGuid(),
            executionRunId,
            "test",
            toolName,
            "ReadOnlyWorkspace",
            "NotRequired",
            "Test receipt.",
            requestSummary,
            ".",
            "Succeeded (exit 0)",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
}
