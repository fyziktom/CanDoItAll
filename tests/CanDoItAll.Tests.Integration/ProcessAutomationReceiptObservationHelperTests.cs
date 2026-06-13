using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessAutomationReceiptObservationHelperTests
{
    [Fact]
    public void ResolveSuccessfulToolNames_uses_process_snapshots_only()
    {
        var detail = CreateDetail(
        [
            CreateReceipt("Workspace-Process", "workspace-write-file", "Succeeded"),
            CreateReceipt("Workspace-Process", "workspace_dotnet_build", "Failed: build errors"),
            CreateReceipt("Browser", "browser_navigate", "Denied: policy")
        ]);

        var successfulToolNames = ProcessAutomationReceiptObservationHelper.ResolveSuccessfulToolNames(detail);

        Assert.Contains("workspace_write_file", successfulToolNames);
        Assert.DoesNotContain("workspace_dotnet_build", successfulToolNames);
        Assert.DoesNotContain("browser_navigate", successfulToolNames);
    }

    [Fact]
    public void ResolveReceiptFamilies_groups_receipts_by_normalized_family()
    {
        var detail = CreateDetail(
        [
            CreateReceipt("Workspace-Process", "workspace_write_file", "Succeeded"),
            CreateReceipt("workspace-process", "workspace_read_file", "Succeeded"),
            CreateReceipt("Browser", "browser_navigate", "Succeeded")
        ]);

        var families = ProcessAutomationReceiptObservationHelper.ResolveReceiptFamilies(detail);

        Assert.Equal(2, families["workspace-process"].Count);
        Assert.Single(families["browser"]);
    }

    [Fact]
    public void ResolveProviderMetadata_reports_provider_metadata_for_successful_receipts_only()
    {
        var detail = CreateDetail(
        [
            CreateReceipt("Workspace-Process", "workspace_write_file", "Succeeded") with
            {
                RuntimeToolProviderKey = "workspace-write",
                RuntimeToolProviderName = "Workspace Write"
            },
            CreateReceipt("Workspace-Process", "workspace_read_file", "Failed: missing file") with
            {
                RuntimeToolProviderKey = "workspace-read",
                RuntimeToolProviderName = "Workspace Read"
            }
        ]);

        var metadata = ProcessAutomationReceiptObservationHelper.ResolveProviderMetadata(detail);

        var item = Assert.Single(metadata);
        Assert.Equal("workspace_write_file", item.ToolName);
        Assert.Equal("workspace-write", item.RuntimeToolProviderKey);
        Assert.Equal("Workspace Write", item.RuntimeToolProviderName);
    }

    private static ProcessAutomationExecutionRunDetail CreateDetail(
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> receipts)
    {
        var now = DateTimeOffset.UtcNow;
        var agentId = Guid.NewGuid();
        var executionRunId = Guid.NewGuid();

        return new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                executionRunId,
                agentId,
                ChatSessionId: null,
                Title: "Process automation execution",
                SourceKind: "process-step",
                SourceId: "step-run-001",
                CorrelationId: "correlation-001",
                CausationId: "causation-001",
                RequestedBy: "process-automation-dispatch",
                RequestedByKind: "system",
                MetadataJson: "{}",
                InputSummary: "Run the process step.",
                ResultSummary: "Completed.",
                ProviderName: "OpenAI",
                Model: "gpt-5-mini",
                State: ProcessAutomationExecutionState.Completed,
                Outcome: ProcessAutomationRunOutcome.Succeeded,
                CreatedAtUtc: now,
                UpdatedAtUtc: now,
                StartedAtUtc: now,
                CompletedAtUtc: now,
                RuntimeSessionKey: string.Empty,
                SerializedSessionStateJson: null,
                PendingApprovals: []),
            ChatSession: null,
            ExecutionLog: [],
            Metrics: [])
        {
            ToolReceipts = receipts
        };
    }

    private static ProcessAutomationToolExecutionReceipt CreateReceipt(
        string toolFamily,
        string toolName,
        string exitSummary)
    {
        var now = DateTimeOffset.UtcNow;
        return new ProcessAutomationToolExecutionReceipt(
            Guid.NewGuid(),
            Guid.NewGuid(),
            toolFamily,
            toolName,
            RiskClass: "medium",
            ApprovalMode: "auto",
            IsolationGuarantee: "workspace",
            RequestSummary: "Run tool",
            WorkingDirectory: "C:\\repo",
            exitSummary,
            StartedAtUtc: now,
            CompletedAtUtc: now);
    }
}
