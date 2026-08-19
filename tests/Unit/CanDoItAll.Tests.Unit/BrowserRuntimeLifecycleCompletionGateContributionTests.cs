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
