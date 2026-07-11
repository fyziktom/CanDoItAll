using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessInteractiveUiAcceptanceGateTests
{
    private static readonly Guid ExecutionRunId = Guid.NewGuid();
    private static readonly DateTimeOffset StartedAtUtc = new(2026, 7, 10, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Validate_ReachabilityOnly_ReturnsProductSourceInspectionIssue()
    {
        var context = CreateContext(
        [
            Receipt(ToolContractCatalog.BrowserNavigate, "http://127.0.0.1:5000", 1),
            Receipt(ToolContractCatalog.BrowserEvaluate, "filename=state.json", 2),
            Receipt(ToolContractCatalog.BrowserSnapshot, "filename=state.yml", 3)
        ]);

        var issue = new ProcessInteractiveUiAcceptanceGate().Validate(context);

        Assert.NotNull(issue);
        Assert.Equal(ProcessCompletionDiagnosticCodes.ProductSourceInspectionEvidenceMissing, issue.Code);
    }

    [Fact]
    public void Validate_ProductReadWithoutInteraction_ReturnsInteractionIssue()
    {
        var context = CreateContext(
        [
            Receipt(ToolContractCatalog.WorkspaceReadFile, "external-target/C/work/product/src/App/Home.razor", 1),
            Receipt(ToolContractCatalog.BrowserNavigate, "http://127.0.0.1:5000", 2),
            Receipt(ToolContractCatalog.BrowserEvaluate, "filename=state.json", 3),
            Receipt(ToolContractCatalog.BrowserSnapshot, "filename=state.yml", 4)
        ]);

        var issue = new ProcessInteractiveUiAcceptanceGate().Validate(context);

        Assert.NotNull(issue);
        Assert.Equal(ProcessCompletionDiagnosticCodes.UiInteractionEvidenceMissing, issue.Code);
    }

    [Fact]
    public void Validate_InteractionWithoutLaterState_ReturnsPostInteractionStateIssue()
    {
        var context = CreateContext(
        [
            Receipt(ToolContractCatalog.WorkspaceReadFile, "external-target/C/work/product/src/App/Home.razor", 1),
            Receipt(ToolContractCatalog.BrowserNavigate, "http://127.0.0.1:5000", 2),
            Receipt(ToolContractCatalog.BrowserEvaluate, "filename=before.json", 3),
            Receipt(ToolContractCatalog.BrowserSnapshot, "filename=before.yml", 4),
            Receipt(ToolContractCatalog.BrowserPressKey, "key=ArrowRight", 5)
        ]);

        var issue = new ProcessInteractiveUiAcceptanceGate().Validate(context);

        Assert.NotNull(issue);
        Assert.Equal(ProcessCompletionDiagnosticCodes.UiPostInteractionStateEvidenceMissing, issue.Code);
    }

    [Fact]
    public void Validate_ProductReadInteractionAndLaterState_ReturnsNoIssue()
    {
        var context = CreateContext(
        [
            Receipt(ToolContractCatalog.WorkspaceReadFile, "external-target/C/work/product/src/App/Home.razor", 1),
            Receipt(ToolContractCatalog.BrowserNavigate, "http://127.0.0.1:5000", 2),
            Receipt(ToolContractCatalog.BrowserSnapshot, "filename=before.yml", 3),
            Receipt(ToolContractCatalog.BrowserPressKey, "key=ArrowRight", 4),
            Receipt(ToolContractCatalog.BrowserEvaluate, "filename=after.json", 5),
            Receipt(ToolContractCatalog.BrowserTakeScreenshot, "filename=after.png", 6)
        ]);

        var issue = new ProcessInteractiveUiAcceptanceGate().Validate(context);

        Assert.Null(issue);
    }

    private static ProcessCompletionGateContext CreateContext(IReadOnlyList<ToolExecutionReceiptRecord> receipts)
    {
        var assignment = new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "qa-validation",
            "qa-lead",
            "qa-lead",
            "QA lead",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            "QA agent",
            "Validate the visible workflow.",
            "sha256:readiness",
            "Test assignment",
            [ArtifactSlotId.New()],
            [],
            ["RunValidation", "CaptureRuntimeProof"],
            "ExternalProductTargetReadOnly",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProcessRuntimeLaunchVariables.ProductRootAlias] = "external-target/C/work/product",
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep] =
                    JsonSerializer.Serialize(new Dictionary<string, object[]>
                    {
                        ["qa-validation"] =
                        [
                            BranchReceipt(ToolContractCatalog.BrowserNavigate),
                            BranchReceipt(ToolContractCatalog.BrowserEvaluate),
                            BranchReceipt(ToolContractCatalog.BrowserSnapshot)
                        ]
                    })
            },
            BranchGate: null,
            DateTimeOffset.UtcNow);
        return new ProcessCompletionGateContext(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Interactive acceptance claimed.",
                BranchOutcomeKey = "quality-accepted",
                EvidenceRefs = [],
                NextActions = []
            },
            receipts,
            ExecutionRunId);
    }

    private static Dictionary<string, object> BranchReceipt(string toolName)
        => new(StringComparer.Ordinal)
        {
            ["toolName"] = toolName,
            ["purpose"] = "AcceptanceProof",
            ["applicableBranchOutcomeKeys"] = new[] { "quality-accepted" }
        };

    private static ToolExecutionReceiptRecord Receipt(string toolName, string requestSummary, int second)
    {
        var observedAtUtc = StartedAtUtc.AddSeconds(second);
        return new ToolExecutionReceiptRecord(
            Guid.NewGuid(),
            ExecutionRunId,
            "test",
            toolName,
            "ReadOnlyWorkspace",
            "NotRequired",
            "Test receipt.",
            requestSummary,
            ".",
            "Succeeded (exit 0)",
            observedAtUtc,
            observedAtUtc);
    }
}
