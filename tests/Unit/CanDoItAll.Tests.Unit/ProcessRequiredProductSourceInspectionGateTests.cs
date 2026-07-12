using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceProductSourceInspectionCompletionGateContributionTests
{
    private static readonly Guid ExecutionRunId = Guid.NewGuid();

    [Fact]
    public void Validate_RequiredDiagnosisWithoutProductRead_ReturnsIssue()
    {
        var context = CreateContext(
        [
            Receipt("artifacts/process-runs/run/steps/qa-validation.md")
        ]);

        var issue = new WorkspaceProductSourceInspectionCompletionGateContribution().Validate(context);

        Assert.NotNull(issue);
        Assert.Equal(ProcessCompletionDiagnosticCodes.ProductSourceInspectionEvidenceMissing, issue.Code);
    }

    [Fact]
    public void Validate_RequiredDiagnosisWithCurrentProductRead_ReturnsNoIssue()
    {
        var context = CreateContext(
        [
            Receipt("external-target/C/work/product/src/App/Pages/Home.razor")
        ],
        excludeShellFiles: true);

        var issue = new WorkspaceProductSourceInspectionCompletionGateContribution().Validate(context);

        Assert.Null(issue);
    }

    [Fact]
    public void Validate_OwningSourceRequired_RejectsShellStyleRead()
    {
        var context = CreateContext(
        [
            Receipt("external-target/C/work/product/src/App/wwwroot/css/app.css")
        ],
        excludeShellFiles: true);

        var issue = new WorkspaceProductSourceInspectionCompletionGateContribution().Validate(context);

        Assert.NotNull(issue);
        Assert.Equal(ProcessCompletionDiagnosticCodes.ProductSourceInspectionEvidenceMissing, issue.Code);
        Assert.Contains("owning product file", issue.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("app.css", issue.Summary, StringComparison.Ordinal);
        Assert.Contains("'/wwwroot/'", issue.Summary, StringComparison.Ordinal);
        Assert.Contains("List or search", issue.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_QualityDiagnosis_RejectsExcludedProgramBootstrapRead()
    {
        var context = CreateContext(
        [
            Receipt("external-target/C/work/product/src/App/Program.cs")
        ],
        excludedPathFragments: ["/Layout/", "/wwwroot/", "/Program.cs", "/App.razor", ".csproj"]);

        var issue = new WorkspaceProductSourceInspectionCompletionGateContribution().Validate(context);

        Assert.NotNull(issue);
        Assert.Equal(ProcessCompletionDiagnosticCodes.ProductSourceInspectionEvidenceMissing, issue.Code);
        Assert.Contains("Program.cs", issue.Summary, StringComparison.Ordinal);
        Assert.Contains("'/Program.cs'", issue.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RepairRequiredBranchWithoutOwningSource_ReturnsNoIssue()
    {
        var context = CreateContext(
            [Receipt("external-target/C/work/product/src/App/wwwroot/css/app.css")],
            excludeShellFiles: true,
            branchOutcomeKey: "repair-required",
            sourceInspectionBranchOutcomeKeys: ["quality-accepted"]);

        var issue = new WorkspaceProductSourceInspectionCompletionGateContribution().Validate(context);

        Assert.Null(issue);
    }

    [Fact]
    public void Validate_AcceptedBranchWithoutOwningSource_ReturnsIssue()
    {
        var context = CreateContext(
            [Receipt("external-target/C/work/product/src/App/wwwroot/css/app.css")],
            excludeShellFiles: true,
            branchOutcomeKey: "quality-accepted",
            sourceInspectionBranchOutcomeKeys: ["quality-accepted"]);

        var issue = new WorkspaceProductSourceInspectionCompletionGateContribution().Validate(context);

        Assert.NotNull(issue);
        Assert.Equal(ProcessCompletionDiagnosticCodes.ProductSourceInspectionEvidenceMissing, issue.Code);
    }

    [Theory]
    [InlineData(ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredStepKeys)]
    [InlineData(ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredBranchOutcomeKeysByStep)]
    [InlineData(ProcessRuntimeLaunchVariables.ProductSourceInspectionExcludedPathFragmentsByStep)]
    public void Validate_MalformedPolicyJson_ReturnsDeterministicConfigurationIssue(string variableName)
    {
        var context = CreateContext(
            [Receipt("external-target/C/work/product/src/App/Pages/Home.razor")],
            policyOverrides: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [variableName] = "{"
            });

        var issue = new WorkspaceProductSourceInspectionCompletionGateContribution().Validate(context);

        Assert.NotNull(issue);
        Assert.Equal("process.runtime.product_source_inspection_policy_invalid", issue.Code);
        Assert.Equal(ProcessDiagnosticRetrySafety.UnsafeToRetry, issue.RetrySafety);
        Assert.Contains(variableName, issue.Summary, StringComparison.Ordinal);
        Assert.Contains(variableName, issue.Evidence, StringComparison.Ordinal);
    }

    private static ProcessCompletionGateContext CreateContext(
        IReadOnlyList<ToolExecutionReceiptRecord> receipts,
        bool excludeShellFiles = false,
        string branchOutcomeKey = "",
        IReadOnlyList<string>? sourceInspectionBranchOutcomeKeys = null,
        IReadOnlyList<string>? excludedPathFragments = null,
        IReadOnlyDictionary<string, string>? policyOverrides = null)
    {
        var launchVariables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProcessRuntimeLaunchVariables.ProductRootAlias] = "external-target/C/work/product",
            [ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredStepKeys] =
                JsonSerializer.Serialize(new[] { "diagnose-quality-failure" })
        };
        if (excludeShellFiles || excludedPathFragments is not null)
        {
            launchVariables[ProcessRuntimeLaunchVariables.ProductSourceInspectionExcludedPathFragmentsByStep] =
                JsonSerializer.Serialize(new Dictionary<string, string[]>
                {
                    ["diagnose-quality-failure"] = excludedPathFragments?.ToArray() ?? ["/Layout/", "/wwwroot/"]
                });
        }
        if (sourceInspectionBranchOutcomeKeys is not null)
        {
            launchVariables[ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredBranchOutcomeKeysByStep] =
                JsonSerializer.Serialize(new Dictionary<string, IReadOnlyList<string>>
                {
                    ["diagnose-quality-failure"] = sourceInspectionBranchOutcomeKeys
                });
        }

        if (policyOverrides is not null)
        {
            foreach (var policyOverride in policyOverrides)
            {
                launchVariables[policyOverride.Key] = policyOverride.Value;
            }
        }

        var assignment = new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "diagnose-quality-failure",
            "repair-manager",
            "repair-manager",
            "Repair manager",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            "Repair manager",
            "Diagnose the failure.",
            "sha256:readiness",
            "Test assignment",
            [ArtifactSlotId.New()],
            [],
            ["ReadUpstreamArtifacts"],
            "ExternalProductTargetReadOnly",
            launchVariables,
            BranchGate: null,
            DateTimeOffset.UtcNow);
        return new ProcessCompletionGateContext(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                BranchOutcomeKey = branchOutcomeKey,
                Reason = "Diagnosis complete.",
                EvidenceRefs = [],
                NextActions = []
            },
            receipts,
            ExecutionRunId);
    }

    private static ToolExecutionReceiptRecord Receipt(string path)
    {
        var now = DateTimeOffset.UtcNow;
        return new ToolExecutionReceiptRecord(
            Guid.NewGuid(),
            ExecutionRunId,
            "workspace-file",
            ToolContractCatalog.WorkspaceReadFile,
            "ReadOnlyWorkspace",
            "NotRequired",
            "Workspace read.",
            path,
            ".",
            "Succeeded: Read file.",
            now,
            now);
    }
}
