using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRuntimePolicyIsolationTests
{
    [Fact]
    public void Tool_receipt_catalog_composes_generic_and_dotnet_semantics()
    {
        var catalog = CreateToolReceiptPolicyCatalog();
        var receipt = CreateToolReceipt("workspace_dotnet_new", "new blazorwasm --name SampleApp");

        var match = catalog.MatchRequirement(receipt, "template=blazorwasm");

        Assert.True(match.IsHandled);
        Assert.True(match.IsMatch);
        Assert.True(catalog.IsProductMutationTool("workspace_write_file"));
        Assert.True(catalog.IsProductMutationTool("workspace_dotnet_new"));
        Assert.True(catalog.IsProductValidationTool("workspace_dotnet_build"));
        Assert.False(catalog.IsProductValidationTool("workspace_npm_install"));
        Assert.False(catalog.IsProductMutationTool("browser_navigate"));
    }

    [Fact]
    public void Tool_receipt_catalog_rejects_ambiguous_policy_ownership()
    {
        var catalog = new ProcessToolReceiptPolicyCatalog(
        [
            new AlwaysMatchingToolReceiptPolicyContribution(),
            new AlwaysMatchingToolReceiptPolicyContribution()
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            catalog.MatchRequirement(CreateToolReceipt("custom_tool", "custom"), "custom-requirement"));

        Assert.Contains("Multiple process tool receipt policies", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Subprocess_contract_resolver_uses_domain_provider_without_generic_defaults()
    {
        var genericResolver = new ProcessSubprocessContractResolver([]);
        var dotNetResolver = new ProcessSubprocessContractResolver(
            [new DotNetSoftwareDeliverySubprocessContractProvider()]);
        var launchVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProcessRuntimeLaunchVariables.ProcessDefinitionKey] = "software-delivery",
            [ProcessRuntimeLaunchVariables.ProcessStepSubprocessDefinitionKey] = "dotnet-development-slice"
        };

        Assert.False(genericResolver.TryResolve(launchVariables, "implementation", out _));
        Assert.True(dotNetResolver.TryResolve(launchVariables, "implementation", out var contract));
        Assert.Equal("dotnet-development-slice", contract.DefinitionKey);
    }

    [Fact]
    public void Processes_module_registers_policy_catalogs_at_composition_root()
    {
        var services = new ServiceCollection();

        services.AddProcessesModule(new ConfigurationBuilder().Build());

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProcessToolReceiptPolicyContribution));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ProcessToolReceiptPolicyCatalog));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProcessSubprocessContractProvider));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ProcessSubprocessContractResolver));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProcessRuntimeToolPlanGuard));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IProcessToolReceiptEvidencePolicyContribution));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ProcessToolReceiptEvidenceGate));
    }

    [Fact]
    public void Dotnet_browser_evidence_gate_rejects_current_run_blazor_error_banner()
    {
        var issue = EvaluateBrowserSnapshotEvidence(
            $"- generic: Tetris{Environment.NewLine}- generic: {DotNetBrowserSnapshotEvidencePolicyContribution.BlazorUnhandledErrorBanner}",
            receiptBelongsToCurrentExecution: true,
            processDefinitionKey: "software-delivery");

        Assert.NotNull(issue);
        Assert.Equal(ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected, issue.Code);
        Assert.Contains("visible unhandled-error banner", issue.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dotnet_browser_evidence_gate_accepts_clean_current_run_snapshot()
    {
        var issue = EvaluateBrowserSnapshotEvidence(
            "- generic: Tetris playable session ready",
            receiptBelongsToCurrentExecution: true,
            processDefinitionKey: "software-delivery");

        Assert.Null(issue);
    }

    [Fact]
    public void Dotnet_browser_evidence_gate_rejects_fatal_banner_during_mutation_capable_quality_repair()
    {
        var issue = EvaluateBrowserSnapshotEvidence(
            $"- generic: {DotNetBrowserSnapshotEvidencePolicyContribution.BlazorUnhandledErrorBanner}",
            receiptBelongsToCurrentExecution: true,
            processDefinitionKey: "software-delivery",
            stepKey: "quality-repair",
            branchOutcomeKey: string.Empty);

        Assert.NotNull(issue);
        Assert.Equal(ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected, issue.Code);
    }

    [Fact]
    public void Dotnet_browser_evidence_gate_ignores_previous_execution_snapshot()
    {
        var issue = EvaluateBrowserSnapshotEvidence(
            $"- generic: {DotNetBrowserSnapshotEvidencePolicyContribution.BlazorUnhandledErrorBanner}",
            receiptBelongsToCurrentExecution: false,
            processDefinitionKey: "software-delivery");

        Assert.Null(issue);
    }

    [Fact]
    public void Dotnet_browser_evidence_policy_does_not_apply_to_generic_processes()
    {
        var issue = EvaluateBrowserSnapshotEvidence(
            $"- generic: {DotNetBrowserSnapshotEvidencePolicyContribution.BlazorUnhandledErrorBanner}",
            receiptBelongsToCurrentExecution: true,
            processDefinitionKey: "generic-browser-review");

        Assert.Null(issue);
    }

    private static ProcessToolReceiptPolicyCatalog CreateToolReceiptPolicyCatalog()
        => new(
        [
            new GenericWorkspaceToolReceiptPolicyContribution(),
            new DotNetToolReceiptPolicyContribution()
        ]);

    private static ToolExecutionReceiptRecord CreateToolReceipt(string toolName, string requestSummary)
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test-provider",
            toolName,
            "ReadOnlyWorkspace",
            "NotRequired",
            "Policy isolation test.",
            requestSummary,
            ".",
            "Succeeded (exit code 0).",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private static ProcessCompletionIssue? EvaluateBrowserSnapshotEvidence(
        string content,
        bool receiptBelongsToCurrentExecution,
        string processDefinitionKey,
        string stepKey = "qa-validation",
        string branchOutcomeKey = "quality-accepted")
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"CanDoItAll.ProcessEvidenceGate.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        try
        {
            var workspaceFiles = new WorkspaceFileService(workspaceRoot);
            var runId = new ProcessRunId(Guid.NewGuid());
            var executionRunId = Guid.NewGuid();
            var artifactPath = $"artifacts/process-runs/{runId.Value:D}/qa-browser-snapshot.yml";
            var writeResult = workspaceFiles.WriteTextFile(artifactPath, content);
            Assert.True(writeResult.Succeeded, writeResult.Message);
            var assignment = new ProcessRuntimeStepAssignment(
                runId,
                ProcessInstancePlanId.New(),
                ProcessStepInstanceId.New(),
                stepKey,
                "qa",
                "qa",
                "QA",
                "Agent",
                Guid.NewGuid().ToString("D"),
                "QA",
                "Validate browser evidence.",
                "sha256:readiness",
                "Test assignment.",
                [ArtifactSlotId.New()],
                [],
                [],
                "ExternalProductTargetReadOnly",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessRuntimeLaunchVariables.ProcessDefinitionKey] = processDefinitionKey
                },
                BranchGate: null,
                DateTimeOffset.UtcNow);
            var output = new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "QA accepted the browser evidence.",
                BranchOutcomeKey = branchOutcomeKey,
                EvidenceRefs = [artifactPath],
                NextActions = [],
                HumanReadableSummaryMarkdown = "Status: Completed"
            };
            var receiptExecutionRunId = receiptBelongsToCurrentExecution
                ? executionRunId
                : Guid.NewGuid();
            var receipt = CreateToolReceipt(
                "browser_snapshot",
                $"boxes=False, depth=2, filename=\"{artifactPath}\"") with
            {
                ExecutionRunId = receiptExecutionRunId
            };
            var gate = new ProcessToolReceiptEvidenceGate(
                workspaceFiles,
                [new DotNetBrowserSnapshotEvidencePolicyContribution()]);

            return gate.Validate(new ProcessCompletionGateContext(
                assignment,
                output,
                [receipt],
                executionRunId));
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private sealed class AlwaysMatchingToolReceiptPolicyContribution : IProcessToolReceiptPolicyContribution
    {
        public bool IsProductMutationTool(string toolName)
            => false;

        public bool IsProductValidationTool(string toolName)
            => false;

        public ProcessToolReceiptRequirementMatch MatchRequirement(
            ToolExecutionReceiptRecord receipt,
            string requirement)
            => ProcessToolReceiptRequirementMatch.Matched;

        public IEnumerable<string> EnumerateRequirementSearchTerms(string requirement)
            => [];

        public bool TryResolveScriptHelper(
            ProcessRuntimeStepAssignment assignment,
            out ProcessScriptHelperDescriptor descriptor)
        {
            descriptor = null!;
            return false;
        }

        public bool AllowsCompletedOutcomeWithDeclaredBlockers(ProcessRuntimeStepAssignment assignment)
            => false;
    }
}
